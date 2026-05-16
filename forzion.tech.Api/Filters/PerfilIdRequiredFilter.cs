using forzion.tech.Application.Interfaces;

namespace forzion.tech.Api.Filters;

/// <summary>
/// Rejeita requisições autenticadas cujo token não carrega o claim 'perfil_id'.
/// Aplicado no nível do grupo, evita repetição em cada endpoint.
/// </summary>
public sealed class PerfilIdRequiredFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var userContext = context.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (userContext.PerfilId == Guid.Empty)
            return Results.Unauthorized();
        return await next(context);
    }
}
