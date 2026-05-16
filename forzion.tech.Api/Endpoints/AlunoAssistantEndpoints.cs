using System.Diagnostics;
using forzion.tech.AI.Agents;
using forzion.tech.AI.GuardRails;
using forzion.tech.AI.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
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
        ForzionAiMetrics metrics,
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
        {
            metrics.BudgetExceeded.Add(1, new KeyValuePair<string, object?>("agent_type", "aluno"));
            return Results.StatusCode(429);
        }

        // 3. Injection detection — log apenas, não bloquear (usuário autenticado)
        var (injected, pattern) = PromptInjectionPatterns.Check(normalized);
        if (injected)
        {
            logger.LogWarning("InjectionPattern AlunoId={AlunoId} Pattern={Pattern}", alunoId, pattern);
            metrics.InjectionDetected.Add(1, new KeyValuePair<string, object?>("agent_type", "aluno"));
        }

        // 4. Build agente MAF + sessão por request (stateless)
        var agent = registry.GetAlunoAssistant(alunoId);
        var session = await agent.CreateSessionAsync(ct);

        // 5. Timeout: 60s
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        var sw = Stopwatch.StartNew();
        Microsoft.Agents.AI.AgentResponse response;
        try
        {
            response = await agent.RunAsync(normalized, session, AlunoAssistantAgent.DefaultRunOptions, linked.Token);
            sw.Stop();
        }
        catch (OperationCanceledException ex)
        {
            sw.Stop();
            metrics.AgentErrors.Add(1,
                new KeyValuePair<string, object?>("agent_type", "aluno"),
                new KeyValuePair<string, object?>("error_type", "timeout"));
            logger.LogWarning(ex, "AgentTimeout AlunoId={AlunoId}", alunoId);
            return Results.StatusCode(504);
        }
        catch (Exception ex)
        {
            sw.Stop();
            metrics.AgentErrors.Add(1,
                new KeyValuePair<string, object?>("agent_type", "aluno"),
                new KeyValuePair<string, object?>("error_type", "unhandled"));
            logger.LogError(ex, "AgentError AlunoId={AlunoId}", alunoId);
            return Results.StatusCode(500);
        }

        var outputText = response.Text ?? "";
        var actualTokens = (int)(response.Usage?.TotalTokenCount ?? estimatedTokens);

        // 6. Output scan
        var scan = OutputScanner.Scan(outputText);
        if (scan.HasCritical)
        {
            logger.LogError("CriticalOutputFinding AlunoId={AlunoId} Findings={Findings}", alunoId, string.Join(",", scan.Findings));
            return Results.Ok(new { reply = "Não consegui gerar uma resposta adequada. Tente reformular." });
        }

        // 7. Sanitize + commit budget + record metrics
        var safeOutput = OutputSanitizer.SanitizeMarkdown(outputText);
        await budget.CommitAsync(alunoId, AgentType.Aluno, actualTokens, ct);

        metrics.TokensUsed.Add(actualTokens, new KeyValuePair<string, object?>("agent_type", "aluno"));
        metrics.OperationDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("agent_type", "aluno"));

        if (string.IsNullOrWhiteSpace(safeOutput))
        {
            metrics.EmptyReplies.Add(1, new KeyValuePair<string, object?>("agent_type", "aluno"));
            logger.LogWarning("EmptyReply AlunoId={AlunoId}", alunoId);
            return Results.Ok(new { reply = "Não encontrei informações para responder sua pergunta. Verifique com seu treinador se sua ficha de treino está configurada." });
        }

        logger.LogInformation("AgentRun AlunoId={AlunoId} Tokens={Tokens}", alunoId, actualTokens);

        return Results.Ok(new { reply = safeOutput });
    }
}

public sealed record ChatRequest(string Message);
