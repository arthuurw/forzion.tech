using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    IServiceScopeFactory scopeFactory,
    OutboxDurabilityRegistry outboxDurabilidade,
    ILogger<DomainEventDispatcher> logger,
    IHostApplicationLifetime? appLifetime = null,
    BestEffortConcurrencyGate? bestEffortGate = null) : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
            AgendarBestEffort(domainEvent);

        return Task.CompletedTask;
    }

    private void AgendarBestEffort(IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        ExecutarEmBackground(async cancellationToken =>
        {
            var gateAdquirido = false;
            try
            {
                if (bestEffortGate is not null)
                {
                    await bestEffortGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    gateAdquirido = true;
                }

                using var scope = scopeFactory.CreateScope();
                foreach (var handler in scope.ServiceProvider.GetServices(handlerType).OfType<IDomainEventHandlerBase>())
                {
                    if (outboxDurabilidade.EhHandlerDuravel(eventType, handler.GetType()))
                        continue;

                    try
                    {
                        await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                    {
                        logger.LogDebug(ex, "Best-effort de {EventType} cancelado no shutdown.", eventType.Name);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Falha ao processar domain event {EventType} no handler {HandlerType}.",
                            eventType.Name,
                            handler.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Best-effort de {EventType} cancelado aguardando capacidade no shutdown.", eventType.Name);
            }
            finally
            {
                if (gateAdquirido)
                    bestEffortGate!.Release();
            }
        });
    }

    protected virtual void ExecutarEmBackground(Func<CancellationToken, Task> trabalho) =>
        _ = Task.Run(() => trabalho(appLifetime?.ApplicationStopping ?? CancellationToken.None));

    public async Task DispatchDuravelAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var handlers = serviceProvider.GetServices(handlerType)
            .OfType<IDomainEventHandlerBase>()
            .Where(h => outboxDurabilidade.EhHandlerDuravel(eventType, h.GetType()))
            .ToList();

        if (handlers.Count == 0)
            throw new InvalidOperationException($"Nenhum handler durável registrado para {eventType.Name}.");

        foreach (var handler in handlers)
            await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
