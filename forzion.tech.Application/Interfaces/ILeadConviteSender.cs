using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces;

/// <summary>
/// Envia o link de convite ao contato do lead. Devolve se o envio de fato saiu — o convite já
/// foi persistido antes desta chamada, então falha aqui nunca desfaz o registro (best-effort).
/// </summary>
public interface ILeadConviteSender
{
    Task<bool> EnviarAsync(
        TipoContatoLead contatoTipo,
        string contatoValor,
        string nomeLead,
        string nomeTreinador,
        string tokenCru,
        CancellationToken cancellationToken = default);
}
