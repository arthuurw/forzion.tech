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
            // Nunca repassa ex.Message adiante: qualquer sink de log (ex.: ErrorLogDbSinkProvider)
            // pode renderizar a mensagem crua, e MascaraPii.Scrub cobre e-mail/telefone via regex mas nao
            // nome proprio — a unica garantia de zero PII aqui e trocar ex por um substituto seguro
            // antes do logger, mantendo o parametro Exception exigido por S6667.
            ex = new Exception(ex.GetType().Name);
            logger.LogError(
                ex,
                "Excecao nao tratada no grupo de agentes — Metodo: {Metodo} Caminho: {Caminho}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value);

            return AgentProblem.Criar(AgentErrorCode.DependencyUnavailable, StatusCodes.Status503ServiceUnavailable);
        }
    }
}
