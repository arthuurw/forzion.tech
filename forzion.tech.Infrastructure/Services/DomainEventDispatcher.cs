using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Infrastructure.Services;

public class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            foreach (var handler in serviceProvider.GetServices(handlerType).OfType<IDomainEventHandlerBase>())
                await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
