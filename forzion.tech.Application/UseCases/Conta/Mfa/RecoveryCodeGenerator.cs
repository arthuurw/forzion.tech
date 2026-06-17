using System.Security.Cryptography;
using System.Text;

namespace forzion.tech.Application.UseCases.Conta.Mfa;

internal static class RecoveryCodeGenerator
{
    public const int Quantidade = 10;

    public static IReadOnlyList<(string Raw, string Hash)> Gerar(int quantidade = Quantidade)
    {
        var lista = new List<(string Raw, string Hash)>(quantidade);
        for (var i = 0; i < quantidade; i++)
        {
            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
            lista.Add((raw, Hash(raw)));
        }

        return lista;
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
