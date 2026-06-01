using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Confirma para o aluno via WhatsApp que sua assinatura foi cancelada.
/// Disparado por <see cref="AssinaturaAlunoCanceladaEvent"/>. Sem telefone do
/// aluno, no-op (e-mail handler cobre o canal de confirmação).
///
/// Treinador recebe notificação separada via
/// <see cref="AssinaturaAlunoCanceladaWhatsAppTreinadorHandler"/>.
/// </summary>
public sealed class AssinaturaAlunoCanceladaWhatsAppAlunoHandler(
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    IOptions<AppSettings> appSettings,
    ILogger<AssinaturaAlunoCanceladaWhatsAppAlunoHandler> logger) : IDomainEventHandler<AssinaturaAlunoCanceladaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCanceladaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoCanceladaWhatsAppAlunoHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("AssinaturaAlunoCanceladaWhatsAppAlunoHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await whatsAppNotifier
            .SendTemplateAsync(
                aluno.Telefone,
                WhatsAppTemplates.AssinaturaCancelada(aluno.Nome, linkPortal),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
