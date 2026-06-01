using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Alerts;

/// <summary>
/// Loga disputa (chargeback) em <see cref="LogLevel.Critical"/> com campos estruturados.
/// Sentry/Loki/qualquer agregador de log usado pelo Arthur picks up automaticamente.
///
/// <para>
/// Decisão: alerting via log estruturado em vez de criar issue no GitHub direto pelo
/// backend — issue requer integração com GitHub API + token, e o canal de log já é
/// suficiente. Se futuramente trocarmos pra Sentry, criar issue na hora vira regra
/// do alert manager, não responsabilidade do código.
/// </para>
/// </summary>
public sealed class PagamentoEmDisputaAlertHandler(
    ILogger<PagamentoEmDisputaAlertHandler> logger) : IDomainEventHandler<PagamentoEmDisputaEvent>
{
    public Task HandleAsync(PagamentoEmDisputaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        logger.LogCritical(
            "CHARGEBACK aberto: pagamento {PagamentoId} (assinatura {AssinaturaAlunoId}), valor R$ {Valor}, motivo {MotivoDisputa}. Treinador deve responder no dashboard.stripe.com em 7-21 dias.",
            domainEvent.PagamentoId,
            domainEvent.AssinaturaAlunoId,
            domainEvent.Valor,
            domainEvent.MotivoDisputa);

        return Task.CompletedTask;
    }
}
