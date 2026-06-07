using forzion.tech.Api.Extensions;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Internal;

public static class InternalEndpoints
{
    // Identidade sintética do job de purga LGPD na auditoria (logs_aprovacao.realizado_por_id).
    // Distinta de qualquer ContaId real → handler segue o ramo não-self (sem senha).
    private static readonly Guid SistemaPurgaLgpdId = new("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapInternalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/lgpd/contas-elegiveis", async (
            HttpContext httpContext,
            [FromServices] ListarContasElegivelPurgaLgpdHandler handler,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!InternalApiKeyValidator.ChaveInternaValida(httpContext, configuration))
                return Results.Unauthorized();

            var ids = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { contas = ids });
        })
        .WithTags("Internal")
        .WithSummary("Lista contas elegíveis para purga LGPD (5 anos pós-cancelamento) — requer X-Internal-Key")
        .AllowAnonymous()
        .RequireRateLimiting("internal")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapDelete("/internal/lgpd/contas/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            [FromServices] AnonimizarContaHandler handler,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!InternalApiKeyValidator.ChaveInternaValida(httpContext, configuration))
                return Results.Unauthorized();

            // RealizadoPorId != ContaId → caminho não-self do handler (sem confirmação de senha),
            // como a anonimização por admin. O job não tem identidade de usuário.
            var result = await handler
                .HandleAsync(new AnonimizarContaCommand(ContaId: id, RealizadoPorId: SistemaPurgaLgpdId, SenhaAtual: null), cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithTags("Internal")
        .WithSummary("Anonimiza conta elegível à purga LGPD (job mensal) — requer X-Internal-Key")
        .AllowAnonymous()
        .RequireRateLimiting("internal")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/processar-pre-avisos", async (
            HttpContext httpContext,
            [FromServices] DespacharPreAvisosAlunoHandler handler,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!InternalApiKeyValidator.ChaveInternaValida(httpContext, configuration))
                return Results.Unauthorized();

            var enviados = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { enviados });
        })
        .WithTags("Internal")
        .WithSummary("Dispara pré-aviso de renovação 3 dias antes para assinaturas de alunos — requer X-Internal-Key")
        .AllowAnonymous()
        .RequireRateLimiting("internal")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/internal/processar-pre-avisos-treinador", async (
            HttpContext httpContext,
            [FromServices] DespacharPreAvisosTreinadorHandler handler,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!InternalApiKeyValidator.ChaveInternaValida(httpContext, configuration))
                return Results.Unauthorized();

            var enviados = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { enviados });
        })
        .WithTags("Internal")
        .WithSummary("Dispara pré-aviso de renovação 3 dias antes para planos de treinadores — requer X-Internal-Key")
        .AllowAnonymous()
        .RequireRateLimiting("internal")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
