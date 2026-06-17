using System.Security.Cryptography;
using System.Text;

namespace forzion.tech.Application.UseCases.Auth.Mfa;

internal static class TrustedDeviceToken
{
    public static (string Raw, string Hash) Gerar()
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return (raw, Hash(raw));
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
