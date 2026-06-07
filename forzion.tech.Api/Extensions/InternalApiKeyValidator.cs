using System.Security.Cryptography;
using System.Text;

namespace forzion.tech.Api.Extensions;

public static class InternalApiKeyValidator
{
    // Constant-time comparison para evitar timing attack na chave interna.
    // FixedTimeEquals lança ArgumentException em spans de tamanhos diferentes — verificar comprimento antes.
    public static bool ChaveInternaValida(HttpContext ctx, IConfiguration cfg)
    {
        var apiKey = cfg["Internal:ApiKey"];
        var headerKey = ctx.Request.Headers["X-Internal-Key"].FirstOrDefault() ?? string.Empty;
        var headerBytes = Encoding.UTF8.GetBytes(headerKey);
        var keyBytes = Encoding.UTF8.GetBytes(apiKey ?? string.Empty);
        return !string.IsNullOrEmpty(apiKey)
            && headerBytes.Length == keyBytes.Length
            && CryptographicOperations.FixedTimeEquals(headerBytes, keyBytes);
    }
}
