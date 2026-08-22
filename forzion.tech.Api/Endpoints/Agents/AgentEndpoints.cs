using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Application.UseCases.Agents.BusinessInfo;
using forzion.tech.Application.UseCases.Agents.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace forzion.tech.Api.Endpoints.Agents;

public static class AgentEndpoints
{
    internal const string Prefixo = "/internal/agents/v1";
    internal const string TagAgentsReady = "agents-ready";

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var grupo = CriarGrupo(endpoints);

        grupo.MapGet("/health", async (
            [FromServices] HealthCheckService saude,
            [FromServices] TimeProvider relogio,
            CancellationToken cancellationToken) =>
        {
            var relatorio = await saude
                .CheckHealthAsync(registro => registro.Tags.Contains(TagAgentsReady), cancellationToken)
                .ConfigureAwait(false);

            // Só o status agregado e o instante saem daqui: nome de check, descrição e exceção
            // descrevem a topologia interna e não vão para o chamador.
            return relatorio.Status switch
            {
                HealthStatus.Unhealthy => AgentProblem.Criar(
                    AgentErrorCode.DependencyUnavailable, StatusCodes.Status503ServiceUnavailable),
                HealthStatus.Degraded => Results.Ok(new AgentHealth("degraded", relogio.GetUtcNow())),
                _ => Results.Ok(new AgentHealth("healthy", relogio.GetUtcNow())),
            };
        });

        grupo.MapGet("/tenants/{tenantId}/business-info", async Task<IResult> (
            string tenantId,
            [FromServices] ObterBusinessInfoHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            var result = await handler.HandleAsync(new ObterBusinessInfoQuery(tenantGuid), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : AgentProblem.Criar(AgentErrorCode.TenantNotFound, StatusCodes.Status404NotFound);
        });

        grupo.MapGet("/tenants/{tenantId}/services", async Task<IResult> (
            string tenantId,
            string? category,
            [FromServices] ListarServicosHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);
            if (category is { Length: > 100 })
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            var result = await handler.HandleAsync(new ListarServicosQuery(tenantGuid, category), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : AgentProblem.Criar(AgentErrorCode.TenantNotFound, StatusCodes.Status404NotFound);
        });

        return endpoints;
    }

    // Assinatura, rate limit e exclusão do OpenAPI ficam no GRUPO: endpoint acrescentado aqui
    // nasce protegido sem declarar nada, e esquecer a anotação deixa de ser uma forma de abrir
    // a superfície. Exposto para o teste montar um endpoint no mesmo grupo que a produção usa.
    internal static RouteGroupBuilder CriarGrupo(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup(Prefixo)
            .AddEndpointFilter<AgentExceptionFilter>()
            .AddEndpointFilter<HmacSignatureFilter>()
            .RequireRateLimiting("agents")
            .AllowAnonymous()
            .ExcludeFromDescription();

    internal sealed record AgentHealth(string Status, DateTimeOffset CheckedAt);
}
