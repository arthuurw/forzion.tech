using System.Text.Json;
using forzion.tech.AI.Agents;
using forzion.tech.AI.GuardRails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Endpoints;

public static class TreinadorAssistantEndpoints
{
    private static readonly TimeSpan DraftTtl = TimeSpan.FromMinutes(10);

    // Cache in-memory de drafts pendentes — Sprint 4: migrar para distributed cache
    private static readonly Dictionary<Guid, (Guid TreinadorId, JsonElement Draft, DateTime ExpiresAt)> PendingDrafts = [];
    private static readonly object DraftLock = new();

    public static void MapTreinadorAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/treinador/assistant")
            .RequireAuthorization("Treinador");

        group.MapPost("/chat", Chat)
            .RequireRateLimiting("agent-treinador");

        group.MapPost("/apply-suggestion", ApplySuggestion)
            .RequireRateLimiting("agent-treinador");
    }

    private static async Task<IResult> Chat(
        [FromBody] ChatRequest req,
        HttpContext ctx,
        AgentRegistry registry,
        ITokenBudget budget,
        ILogger<ApplySuggestionRequest> logger,
        CancellationToken ct)
    {
        var perfilId = ctx.User.FindFirst("perfil_id")?.Value;
        if (!Guid.TryParse(perfilId, out var treinadorId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 2000)
            return Results.BadRequest(new { error = "input_invalido" });

        var normalized = InputNormalizer.NormalizeUnicode(req.Message);
        var estimatedTokens = InputNormalizer.EstimateTokens(normalized);

        if (await budget.WouldExceedDailyAsync(treinadorId, AgentType.Treinador, estimatedTokens, ct))
            return Results.StatusCode(429);

        var (injected, pattern) = PromptInjectionPatterns.Check(normalized);
        if (injected)
            logger.LogWarning("InjectionPattern TreinadorId={Id} Pattern={Pattern}", treinadorId, pattern);

        var agent = registry.GetTreinadorAssistant(treinadorId);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, agent.SystemPrompt),
            new(ChatRole.User, normalized)
        };

        var options = new ChatOptions
        {
            Tools = agent.Tools,
            Temperature = agent.Temperature,
            MaxOutputTokens = agent.MaxOutputTokens
        };

        ChatResponse response;
        try
        {
            response = await agent.Client.GetResponseAsync(messages, options, linked.Token);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "AgentTimeout TreinadorId={Id}", treinadorId);
            return Results.StatusCode(504);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentError TreinadorId={Id}", treinadorId);
            return Results.StatusCode(500);
        }

        var outputText = response.Text ?? "";
        var actualTokens = (int)(response.Usage?.TotalTokenCount ?? estimatedTokens);

        var scan = OutputScanner.Scan(outputText);
        if (scan.HasCritical)
        {
            logger.LogError("CriticalOutputFinding TreinadorId={Id} Findings={F}", treinadorId, string.Join(",", scan.Findings));
            return Results.Ok(new { reply = "Não consegui gerar uma resposta adequada. Tente reformular." });
        }

        var safeOutput = OutputSanitizer.SanitizeMarkdown(outputText);
        await budget.CommitAsync(treinadorId, AgentType.Treinador, actualTokens, ct);
        logger.LogInformation("AgentRun TreinadorId={Id} Tokens={Tokens}", treinadorId, actualTokens);

        // Detectar preview de sugestão no output — registrar draft pendente
        if (outputText.Contains("\"__tipo\": \"sugestao_ficha_preview\""))
        {
            var draftId = Guid.NewGuid();
            try
            {
                var startIdx = outputText.IndexOf('{');
                var endIdx = outputText.LastIndexOf('}') + 1;
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    var jsonPart = outputText[startIdx..endIdx];
                    var draft = JsonSerializer.Deserialize<JsonElement>(jsonPart);
                    lock (DraftLock)
                    {
                        CleanExpiredDrafts();
                        PendingDrafts[draftId] = (treinadorId, draft, DateTime.UtcNow.Add(DraftTtl));
                    }
                    return Results.Ok(new
                    {
                        reply = safeOutput,
                        pendingApproval = true,
                        draftId,
                        draftExpiresAt = DateTime.UtcNow.Add(DraftTtl)
                    });
                }
            }
            catch (JsonException) { /* retornar reply normal */ }
        }

        return Results.Ok(new { reply = safeOutput });
    }

    private static IResult ApplySuggestion(
        [FromBody] ApplySuggestionRequest req,
        HttpContext ctx,
        ILogger<ApplySuggestionRequest> logger)
    {
        var perfilId = ctx.User.FindFirst("perfil_id")?.Value;
        if (!Guid.TryParse(perfilId, out var treinadorId))
            return Results.Unauthorized();

        lock (DraftLock)
        {
            if (!PendingDrafts.TryGetValue(req.DraftId, out var entry))
                return Results.NotFound(new { error = "draft_nao_encontrado" });

            if (entry.TreinadorId != treinadorId)
            {
                logger.LogWarning("ApplySuggestion ownership mismatch DraftId={Id} TreinadorId={Tid}", req.DraftId, treinadorId);
                return Results.Forbid();
            }

            if (entry.ExpiresAt < DateTime.UtcNow)
            {
                PendingDrafts.Remove(req.DraftId);
                return Results.BadRequest(new { error = "draft_expirado" });
            }

            PendingDrafts.Remove(req.DraftId);
        }

        // TODO Sprint 3: chamar handler de criação de treino com dados do draft
        logger.LogInformation("ApplySuggestion TreinadorId={Id} DraftId={DId}", treinadorId, req.DraftId);

        return Results.Ok(new { message = "Sugestão confirmada. Ficha salva com sucesso." });
    }

    private static void CleanExpiredDrafts()
    {
        var expired = PendingDrafts.Where(kv => kv.Value.ExpiresAt < DateTime.UtcNow).Select(kv => kv.Key).ToList();
        foreach (var key in expired) PendingDrafts.Remove(key);
    }
}

public sealed record ApplySuggestionRequest(Guid DraftId);
