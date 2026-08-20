using System.Globalization;
using System.Security.Cryptography;

namespace forzion.tech.Api.Endpoints.Agents.Hmac;

internal static class CanonicalPayload
{
    internal static string Montar(
        string metodo,
        string caminhoComQuery,
        ReadOnlySpan<byte> corpo,
        long timestampUnixSegundos)
    {
        var hashDoCorpo = Convert.ToHexStringLower(SHA256.HashData(corpo));
        var timestamp = timestampUnixSegundos.ToString(CultureInfo.InvariantCulture);

        return $"{metodo.ToUpperInvariant()}\n{caminhoComQuery}\n{hashDoCorpo}\n{timestamp}";
    }
}
