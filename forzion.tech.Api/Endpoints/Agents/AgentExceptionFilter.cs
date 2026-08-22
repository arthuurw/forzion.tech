using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Endpoints.Agents;

internal sealed class AgentExceptionFilter(ILogger<AgentExceptionFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Excecao nao tratada no grupo de agentes — Metodo: {Metodo} Caminho: {Caminho}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value);

            return AgentProblem.Criar(AgentErrorCode.DependencyUnavailable, StatusCodes.Status503ServiceUnavailable);
        }
    }
}
