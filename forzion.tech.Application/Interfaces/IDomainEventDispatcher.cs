using forzion.tech.Domain.Events;

namespace forzion.tech.Application.Interfaces;

public interface IDomainEventDispatcher
{
    // In-memory, best-effort: engole exceção de handler (estado de negócio já persistiu).
    // Handlers duráveis (registrados no OutboxDurabilityRegistry) são PULADOS aqui — rodam no worker.
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default);

    // Worker do outbox: dispara SÓ os handlers duráveis do evento e PROPAGA exceção
    // (o worker decide retry/falha). Oposto do best-effort acima.
    Task DispatchDuravelAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
