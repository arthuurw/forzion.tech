using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o aluno via WhatsApp que sua assinatura foi criada.
/// Disparado por <see cref="AssinaturaAlunoCriadaEvent"/>.
/// </summary>
public sealed class AssinaturaAlunoCriadaWhatsAppHandler(
    IAlunoRepository alunoRepository,
    IPacoteRepository pacoteRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<AssinaturaAlunoCriadaWhatsAppHandler> logger) : IDomainEventHandler<AssinaturaAlunoCriadaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoCriadaWhatsAppHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("AssinaturaAlunoCriadaWhatsAppHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        var pacote = await pacoteRepository
            .ObterPorIdAsync(domainEvent.PacoteId, cancellationToken)
            .ConfigureAwait(false);
        var nomePacote = pacote?.Nome ?? "seu pacote";

        await whatsAppNotifier
            .SendTemplateAsync(
                aluno.Telefone,
                WhatsAppTemplates.AssinaturaCriada(aluno.Nome, nomePacote, domainEvent.Valor),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
