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

public static class AlunoAssistantEndpoints
{
    public static void MapAlunoAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/aluno/assistant")
            .RequireAuthorization("Aluno");

        group.MapPost("/chat", Chat)
            .RequireRateLimiting("agent-aluno");
    }

    private static async Task<IResult> Chat(
        [FromBody] ChatRequest req,
        HttpContext ctx,
        AgentRegistry registry,
        ITokenBudget budget,
        ILogger<ChatRequest> logger,
        CancellationToken ct)
    {
        var perfilId = ctx.User.FindFirst("perfil_id")?.Value;
        if (!Guid.TryParse(perfilId, out var alunoId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 2000)
            return Results.BadRequest(new { error = "input_invalido" });

        // 1. Normalização unicode
        var normalized = InputNormalizer.NormalizeUnicode(req.Message);
        var estimatedTokens = InputNormalizer.EstimateTokens(normalized);

        // 2. Token budget
        if (await budget.WouldExceedDailyAsync(alunoId, AgentType.Aluno, estimatedTokens, ct))
            return Results.StatusCode(429);

        // 3. Injection detection — log apenas, não bloquear (usuário autenticado)
        var (injected, pattern) = PromptInjectionPatterns.Check(normalized);
        if (injected)
            logger.LogWarning("InjectionPattern AlunoId={AlunoId} Pattern={Pattern}", alunoId, pattern);

        // 4. Build agente
        var agent = registry.GetAlunoAssistant(alunoId);

        // 5. Timeout: 60s
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        // 6. Montar conversa com system prompt explícito
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
            logger.LogWarning(ex, "AgentTimeout AlunoId={AlunoId}", alunoId);
            return Results.StatusCode(504);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentError AlunoId={AlunoId}", alunoId);
            return Results.StatusCode(500);
        }

        var outputText = response.Text ?? "";
        var actualTokens = (int)(response.Usage?.TotalTokenCount ?? estimatedTokens);

        // 7. Output scan
        var scan = OutputScanner.Scan(outputText);
        if (scan.HasCritical)
        {
            logger.LogError("CriticalOutputFinding AlunoId={AlunoId} Findings={Findings}", alunoId, string.Join(",", scan.Findings));
            return Results.Ok(new { reply = "Não consegui gerar uma resposta adequada. Tente reformular." });
        }

        // 8. Sanitize + commit budget
        var safeOutput = OutputSanitizer.SanitizeMarkdown(outputText);
        await budget.CommitAsync(alunoId, AgentType.Aluno, actualTokens, ct);

        if (string.IsNullOrWhiteSpace(safeOutput))
        {
            logger.LogWarning("EmptyReply AlunoId={AlunoId}", alunoId);
            return Results.Ok(new { reply = "Não encontrei informações para responder sua pergunta. Verifique com seu treinador se sua ficha de treino está configurada." });
        }

        logger.LogInformation("AgentRun AlunoId={AlunoId} Tokens={Tokens}", alunoId, actualTokens);

        return Results.Ok(new { reply = safeOutput });
    }
}

public sealed record ChatRequest(string Message);
