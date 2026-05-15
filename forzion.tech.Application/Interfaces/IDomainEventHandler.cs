using forzion.tech.Domain.Events;

namespace forzion.tech.Application.Interfaces;

public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T domainEvent, CancellationToken cancellationToken = default);
}
