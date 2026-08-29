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
            // pode renderizar a mensagem crua, e MascaraPii.Scrub cobre e-mail/telefone via regex mas
            // nao nome proprio. Tipo completo + stack trace saem como parametros ESTRUTURADOS (nao
            // via ex.StackTrace do objeto logado — um `new Exception()` nunca lancado tem StackTrace
            // nulo, apagando o diagnostico real). O objeto Exception passado ao logger (exigido por
            // S6667) carrega só o tipo no Message, nunca o texto original.
            var tipoOriginal = ex.GetType().FullName;
            var stackTraceOriginal = ex.StackTrace;
            ex = new Exception(tipoOriginal);
            logger.LogError(
                ex,
                "Excecao nao tratada no grupo de agentes — Tipo: {TipoExcecao} Metodo: {Metodo} Caminho: {Caminho} StackTrace: {StackTrace}",
                tipoOriginal,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value,
                stackTraceOriginal);

            return AgentProblem.Criar(AgentErrorCode.DependencyUnavailable, StatusCodes.Status503ServiceUnavailable);
        }
    }
}
