using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Alerts;

public sealed class PagamentoTreinadorEmDisputaAlertHandler(
    ILogger<PagamentoTreinadorEmDisputaAlertHandler> logger) : IDomainEventHandler<PagamentoTreinadorEmDisputaEvent>
{
    public Task HandleAsync(PagamentoTreinadorEmDisputaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        logger.LogCritical(
            "CHARGEBACK de pagamento de treinador: pagamento {PagamentoTreinadorId} (treinador {TreinadorId}), valor R$ {Valor}. Responder no dashboard.stripe.com em 7-21 dias.",
            domainEvent.PagamentoTreinadorId,
            domainEvent.TreinadorId,
            domainEvent.Valor);
        return Task.CompletedTask;
    }
}
