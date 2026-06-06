using forzion.tech.Application.Interfaces;

namespace forzion.tech.Api.Filters;

public abstract class RequireAssinaturaAtivaFilterBase : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // GETs liberados para preservar acesso ao histórico mesmo em inadimplência (LGPD).
        if (HttpMethods.IsGet(context.HttpContext.Request.Method))
            return await next(context);

        var services = context.HttpContext.RequestServices;
        var userContext = services.GetRequiredService<IUserContext>();

        var inadimplente = await EstaInadimplenteAsync(services, userContext, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (inadimplente)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Assinatura inadimplente",
                detail: "Regularize seu pagamento para continuar usando esta funcionalidade.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = CodigoErro
                });
        }

        return await next(context);
    }

    protected abstract string CodigoErro { get; }

    protected abstract Task<bool> EstaInadimplenteAsync(
        IServiceProvider services,
        IUserContext userContext,
        CancellationToken ct);
}
