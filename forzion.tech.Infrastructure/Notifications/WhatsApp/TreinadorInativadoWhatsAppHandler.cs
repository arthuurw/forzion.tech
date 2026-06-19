using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o treinador via WhatsApp que sua conta foi inativada.
/// Disparado por <see cref="TreinadorInativadoEvent"/>.
/// </summary>
public sealed class TreinadorInativadoWhatsAppHandler(
    ITreinadorRepository treinadorRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<TreinadorInativadoWhatsAppHandler> logger) : IDomainEventHandler<TreinadorInativadoEvent>
{
    public async Task HandleAsync(TreinadorInativadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("TreinadorInativadoWhatsAppHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        if (string.IsNullOrWhiteSpace(treinador.Telefone))
        {
            logger.LogDebug("TreinadorInativadoWhatsAppHandler: treinador {Id} sem telefone — ignorado.", treinador.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        await whatsAppNotifier
            .SendTemplateAsync(treinador.Telefone, WhatsAppTemplates.TreinadorInativado(treinador.Nome), cancellationToken)
            .ConfigureAwait(false);
    }
}
