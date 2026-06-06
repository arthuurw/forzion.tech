using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Api.Filters;

public sealed class RequireAssinaturaTreinadorAtivaFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var userContext = services.GetRequiredService<IUserContext>();

        if (userContext.TipoConta != TipoConta.Treinador)
            return await next(context);

        var assinaturaRepository = services.GetRequiredService<IAssinaturaTreinadorRepository>();
        var assinatura = await assinaturaRepository
            .ObterAtualPorTreinadorAsync(userContext.PerfilId, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (assinatura?.Status == AssinaturaTreinadorStatus.Inadimplente)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Assinatura inadimplente",
                detail: "Regularize seu pagamento para continuar usando esta funcionalidade.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "ASSINATURA_TREINADOR_INADIMPLENTE"
                });
        }

        return await next(context);
    }
}
