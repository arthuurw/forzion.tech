using System.Security.Cryptography;
using System.Text;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Agents.Leads;

public static class IdempotenciaLead
{
    public static string Calcular(string nome, TipoContatoLead tipoContato, string valorContato, string? interesse, string finalidade)
    {
        var canonico = string.Join('|', nome, tipoContato.ToString(), valorContato, interesse ?? string.Empty, finalidade);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico))).ToLowerInvariant();
    }
}
