using System.Diagnostics;
using forzion.tech.AI.Agents;
using forzion.tech.AI.GuardRails;
using forzion.tech.AI.Observability;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Endpoints;

public static class TreinadorAssistantEndpoints
{
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
        IDraftRequestTracker draftTracker,
        ForzionAiMetrics metrics,
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
        {
            metrics.BudgetExceeded.Add(1, new KeyValuePair<string, object?>("agent_type", "treinador"));
            return Results.StatusCode(429);
        }

        var (injected, pattern) = PromptInjectionPatterns.Check(normalized);
        if (injected)
        {
            logger.LogWarning("InjectionPattern TreinadorId={Id} Pattern={Pattern}", treinadorId, pattern);
            metrics.InjectionDetected.Add(1, new KeyValuePair<string, object?>("agent_type", "treinador"));
        }

        // Build agente MAF + sessão por request (stateless)
        var agent = registry.GetTreinadorAssistant(treinadorId);
        var session = await agent.CreateSessionAsync(ct);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        var sw = Stopwatch.StartNew();
        Microsoft.Agents.AI.AgentResponse response;
        try
        {
            response = await agent.RunAsync(normalized, session, TreinadorAssistantAgent.DefaultRunOptions, linked.Token);
            sw.Stop();
        }
        catch (OperationCanceledException ex)
        {
            sw.Stop();
            metrics.AgentErrors.Add(1,
                new KeyValuePair<string, object?>("agent_type", "treinador"),
                new KeyValuePair<string, object?>("error_type", "timeout"));
            logger.LogWarning(ex, "AgentTimeout TreinadorId={Id}", treinadorId);
            return Results.StatusCode(504);
        }
        catch (Exception ex)
        {
            sw.Stop();
            metrics.AgentErrors.Add(1,
                new KeyValuePair<string, object?>("agent_type", "treinador"),
                new KeyValuePair<string, object?>("error_type", "unhandled"));
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

        metrics.TokensUsed.Add(actualTokens, new KeyValuePair<string, object?>("agent_type", "treinador"));
        metrics.OperationDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("agent_type", "treinador"));

        if (string.IsNullOrWhiteSpace(safeOutput) && !draftTracker.PendingDraftId.HasValue)
        {
            metrics.EmptyReplies.Add(1, new KeyValuePair<string, object?>("agent_type", "treinador"));
            logger.LogWarning("EmptyReply TreinadorId={Id}", treinadorId);
            return Results.Ok(new { reply = "Não encontrei informações para responder sua solicitação. Verifique se há alunos vinculados ativos ou tente novamente." });
        }

        logger.LogInformation("AgentRun TreinadorId={Id} Tokens={Tokens}", treinadorId, actualTokens);

        if (draftTracker.PendingDraftId.HasValue)
        {
            return Results.Ok(new
            {
                reply = safeOutput,
                pendingApproval = true,
                draftId = draftTracker.PendingDraftId.Value,
                draftExpiresAt = draftTracker.PendingDraftExpiresAt
            });
        }

        return Results.Ok(new { reply = safeOutput });
    }

    private static async Task<IResult> ApplySuggestion(
        [FromBody] ApplySuggestionRequest req,
        HttpContext ctx,
        IDraftSuggestionService draftService,
        CriarTreinoHandler criarTreinoHandler,
        ILogger<ApplySuggestionRequest> logger,
        CancellationToken ct)
    {
        var perfilId = ctx.User.FindFirst("perfil_id")?.Value;
        if (!Guid.TryParse(perfilId, out var treinadorId))
            return Results.Unauthorized();

        var draft = draftService.GetDraft(req.DraftId, treinadorId);
        if (draft is null)
            return Results.NotFound(new { error = "draft_nao_encontrado_ou_expirado" });

        var objetivo = ParseObjetivo(draft.Objetivo);
        var dificuldade = ParseDificuldade(draft.Dificuldade);
        var nome = BuildNome(draft.Objetivo, draft.Dificuldade);

        try
        {
            var command = new CriarTreinoCommand(
                TreinadorId: treinadorId,
                AlunoId: draft.AlunoId,
                Nome: nome,
                Objetivo: objetivo,
                Dificuldade: dificuldade);

            var result = await criarTreinoHandler.HandleAsync(command, ct);
            draftService.RemoveDraft(req.DraftId);

            logger.LogInformation("ApplySuggestion TreinadorId={TId} DraftId={DId} TreinoId={TrId}",
                treinadorId, req.DraftId, result.TreinoId);

            return Results.Ok(new { treinoId = result.TreinoId, message = "Ficha criada com sucesso." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ApplySuggestion failed TreinadorId={TId} DraftId={DId}", treinadorId, req.DraftId);
            return Results.StatusCode(500);
        }
    }

    private static string BuildNome(string objetivo, string dificuldade)
    {
        var nome = $"Sugestão IA: {objetivo} ({dificuldade})";
        return nome.Length > 100 ? nome[..100] : nome;
    }

    private static ObjetivoTreino ParseObjetivo(string s)
    {
        if (Enum.TryParse<ObjetivoTreino>(s, ignoreCase: true, out var parsed))
            return parsed;

        var lower = s.ToLowerInvariant();
        if (lower.Contains("hipertrofia") || lower.Contains("massa") || lower.Contains("músculo"))
            return ObjetivoTreino.Hipertrofia;
        if (lower.Contains("força") || lower.Contains("forca") || lower.Contains("potência"))
            return ObjetivoTreino.Forca;
        if (lower.Contains("resistência") || lower.Contains("resistencia") || lower.Contains("cardio"))
            return ObjetivoTreino.Resistencia;
        if (lower.Contains("emagrec") || lower.Contains("perda de peso") || lower.Contains("queima"))
            return ObjetivoTreino.Emagrecimento;
        if (lower.Contains("reabilita") || lower.Contains("lesão") || lower.Contains("recuperação"))
            return ObjetivoTreino.Reabilitacao;

        return ObjetivoTreino.Hipertrofia;
    }

    private static DificuldadeTreino ParseDificuldade(string s)
    {
        var lower = s.ToLowerInvariant()
            .Replace("á", "a").Replace("ã", "a").Replace("é", "e").Replace("â", "a")
            .Replace("ç", "c").Replace("ó", "o").Replace("ú", "u").Replace("í", "i");

        if (lower.Contains("avan")) return DificuldadeTreino.Avancado;
        if (lower.Contains("inter")) return DificuldadeTreino.Intermediario;
        return DificuldadeTreino.Iniciante;
    }
}

public sealed record ApplySuggestionRequest(Guid DraftId);
