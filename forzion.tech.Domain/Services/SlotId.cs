using System.Security.Cryptography;
using System.Text;

namespace forzion.tech.Domain.Services;

public static class SlotId
{
    public static string Calcular(Guid treinadorId, Guid pacoteId, DateTime inicioUtc)
    {
        var canonico = $"{treinadorId:N}|{pacoteId:N}|{inicioUtc:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico))).ToLowerInvariant();
    }
}
