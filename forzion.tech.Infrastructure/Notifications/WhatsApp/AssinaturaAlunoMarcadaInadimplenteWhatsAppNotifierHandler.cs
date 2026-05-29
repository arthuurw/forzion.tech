using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// IH.4 — notifica aluno via WhatsApp que sua assinatura foi marcada como
/// inadimplente. Mensagem urgente. Resolução direta via <c>AlunoId</c> do event.
/// Sem telefone, no-op. Link sempre pro portal forzion, nunca Stripe.
/// </summary>
public sealed class AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler(
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    IOptions<AppSettings> appSettings,
    ILogger<AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler> logger) : IDomainEventHandler<AssinaturaAlunoMarcadaInadimplenteEvent>
{
    public async Task HandleAsync(AssinaturaAlunoMarcadaInadimplenteEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorAlunoAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await whatsAppNotifier
            .SendTemplateAsync(
                aluno.Telefone,
                WhatsAppTemplates.AssinaturaInadimplente(aluno.Nome, linkPortal),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
