using System.Security.Cryptography;
using System.Text;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Agents.Agendamentos;

public static class IdempotenciaAgendamento
{
    public static string Calcular(Guid serviceId, string slotId, string nome, TipoContatoLead tipoContato, string contatoNormalizado, string finalidade)
    {
        var canonico = string.Join('|', serviceId.ToString(), slotId, nome, tipoContato.ToString(), contatoNormalizado, finalidade);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico))).ToLowerInvariant();
    }
}
