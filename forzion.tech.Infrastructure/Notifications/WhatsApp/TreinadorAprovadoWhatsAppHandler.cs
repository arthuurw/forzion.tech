using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o treinador via WhatsApp que sua conta foi aprovada.
/// Disparado por <see cref="TreinadorAprovadoEvent"/>.
/// </summary>
public sealed class TreinadorAprovadoWhatsAppHandler(
    ITreinadorRepository treinadorRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<TreinadorAprovadoWhatsAppHandler> logger) : IDomainEventHandler<TreinadorAprovadoEvent>
{
    public async Task HandleAsync(TreinadorAprovadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("TreinadorAprovadoWhatsAppHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        if (string.IsNullOrWhiteSpace(treinador.Telefone))
        {
            logger.LogDebug("TreinadorAprovadoWhatsAppHandler: treinador {Id} sem telefone — ignorado.", treinador.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        await whatsAppNotifier
            .SendTemplateAsync(treinador.Telefone, WhatsAppTemplates.TreinadorAprovado(treinador.Nome), cancellationToken)
            .ConfigureAwait(false);
    }
}
