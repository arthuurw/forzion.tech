namespace forzion.tech.Api.Filters;

public sealed class PaginacaoFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var q = context.HttpContext.Request.Query;

        if (q.TryGetValue("pagina", out var pStr) &&
            (!int.TryParse(pStr, out var p) || p < 1))
            return Results.Problem(
                detail: "O parâmetro 'pagina' deve ser um inteiro >= 1.",
                statusCode: StatusCodes.Status400BadRequest);

        if (q.TryGetValue("tamanhoPagina", out var tpStr) &&
            (!int.TryParse(tpStr, out var tp) || tp < 1 || tp > 100))
            return Results.Problem(
                detail: "O parâmetro 'tamanhoPagina' deve ser um inteiro entre 1 e 100.",
                statusCode: StatusCodes.Status400BadRequest);

        return await next(context);
    }
}
