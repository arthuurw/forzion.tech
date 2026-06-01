using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// G-PAY-3 — notifica aluno via WhatsApp que sua assinatura foi reativada após
/// regularização de pagamento (Inadimplente → Ativa).
/// Disparado por <see cref="AssinaturaAlunoReativadaEvent"/>.
///
/// Resolução: aluno via <c>AlunoId</c>; usa <c>aluno.Telefone</c>. Sem telefone, no-op.
/// Canal controlado por <see cref="IPlanoNotificationPolicy"/> (resolver por treinador).
/// NullWhatsAppNotifier absorve silenciosamente quando integração não configurada.
/// </summary>
public sealed class AssinaturaAlunoReativadaWhatsAppHandler(
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    IOptions<AppSettings> appSettings,
    ILogger<AssinaturaAlunoReativadaWhatsAppHandler> logger) : IDomainEventHandler<AssinaturaAlunoReativadaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoReativadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoReativadaWhatsAppHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("AssinaturaAlunoReativadaWhatsAppHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/assinatura";

        await whatsAppNotifier
            .SendTemplateAsync(
                aluno.Telefone,
                WhatsAppTemplates.AssinaturaReativada(aluno.Nome, linkPortal),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
