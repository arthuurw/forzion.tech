using forzion.tech.Domain.Events;

namespace forzion.tech.Application.Interfaces;

public interface IDomainEventHandlerBase
{
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IDomainEventHandler<in T> : IDomainEventHandlerBase where T : IDomainEvent
{
    Task HandleAsync(T domainEvent, CancellationToken cancellationToken = default);
    Task IDomainEventHandlerBase.HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken) =>
        HandleAsync((T)domainEvent, cancellationToken);
}
