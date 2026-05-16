namespace forzion.tech.Api.Helpers;

public static class UrlValidator
{
    public static bool IsUrlPermitida(string url, string urlBase)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!Uri.TryCreate(urlBase, UriKind.Absolute, out var baseUri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Host != "localhost") return false;

        // Compara origin (scheme + host + port) — previne subdomain bypass e path-prefix bypass
        return uri.GetLeftPart(UriPartial.Authority).Equals(
            baseUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }
}
