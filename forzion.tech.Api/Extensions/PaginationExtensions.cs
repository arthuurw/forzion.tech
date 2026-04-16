namespace forzion.tech.Api.Extensions;

public static class PaginationExtensions
{
    public static PaginationParams ObterPaginacaoDoQuery(this HttpContext httpContext)
    {
        _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
        _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
        
        var p = pagina < 1 ? 1 : pagina;
        var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;
        
        return new PaginationParams(p, tp);
    }
}

public record PaginationParams(int Pagina, int TamanhoPagina);
