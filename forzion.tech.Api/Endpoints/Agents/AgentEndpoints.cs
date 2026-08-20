using forzion.tech.Api.Endpoints.Agents.Hmac;

namespace forzion.tech.Api.Endpoints.Agents;

public static class AgentEndpoints
{
    internal const string Prefixo = "/internal/agents/v1";

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        CriarGrupo(endpoints);

        return endpoints;
    }

    // Assinatura, rate limit e exclusão do OpenAPI ficam no GRUPO: endpoint acrescentado aqui
    // nasce protegido sem declarar nada, e esquecer a anotação deixa de ser uma forma de abrir
    // a superfície. Exposto para o teste montar um endpoint no mesmo grupo que a produção usa.
    internal static RouteGroupBuilder CriarGrupo(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup(Prefixo)
            .AddEndpointFilter<HmacSignatureFilter>()
            .RequireRateLimiting("agents")
            .AllowAnonymous()
            .ExcludeFromDescription();
}
