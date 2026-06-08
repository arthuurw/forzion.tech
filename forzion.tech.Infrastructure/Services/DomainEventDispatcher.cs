using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            foreach (var handler in serviceProvider.GetServices(handlerType).OfType<IDomainEventHandlerBase>())
            {
                try
                {
                    await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Handler de evento é efeito colateral pós-commit (§1 specification-coding):
                    // estado de negócio já persistiu. Falha aqui não pode propagar e derrubar
                    // o CommitAsync da use case — loga e segue para o próximo handler.
                    logger.LogError(
                        ex,
                        "Falha ao processar domain event {EventType} no handler {HandlerType}.",
                        domainEvent.GetType().Name,
                        handler.GetType().Name);
                }
            }
        }
    }
}
