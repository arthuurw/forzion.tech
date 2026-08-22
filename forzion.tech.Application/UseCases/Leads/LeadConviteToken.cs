using System.Security.Cryptography;
using System.Text;

namespace forzion.tech.Application.UseCases.Leads;

public static class LeadConviteToken
{
    public static string Gerar() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
