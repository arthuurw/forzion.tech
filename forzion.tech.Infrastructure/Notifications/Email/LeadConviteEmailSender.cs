using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class LeadConviteEmailSender(
    IEmailService emailService,
    IOptions<AppSettings> appSettings) : ILeadConviteSender
{
    public async Task<bool> EnviarAsync(
        TipoContatoLead contatoTipo,
        string contatoValor,
        string nomeLead,
        string nomeTreinador,
        string tokenCru,
        CancellationToken cancellationToken = default)
    {
        // WhatsApp indisponível em produção hoje (Meta não é subprocessadora ativa) — na prática
        // o convite só sai por e-mail. Telefone já é bloqueado antes de chegar aqui.
        if (contatoTipo != TipoContatoLead.Email)
            return false;

        if (!emailService.Habilitado)
            return false;

        var link = $"{appSettings.Value.FrontendBaseUrl}/cadastro/aluno?convite={tokenCru}";

        await emailService.EnviarAsync(
            contatoValor,
            "Convite para se cadastrar — forzion.tech",
            EmailTemplates.ConviteLead(nomeLead, nomeTreinador, link),
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
