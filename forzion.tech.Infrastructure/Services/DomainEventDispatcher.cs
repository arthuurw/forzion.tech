using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    OutboxDurabilityRegistry outboxDurabilidade,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            var eventType = domainEvent.GetType();
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            foreach (var handler in serviceProvider.GetServices(handlerType).OfType<IDomainEventHandlerBase>())
            {
                // Handler durável roda no worker do outbox (com retry); aqui só as notificações
                // best-effort do mesmo evento. Sem o skip, a mutação rodaria 2× (in-memory + worker).
                if (outboxDurabilidade.EhHandlerDuravel(eventType, handler.GetType()))
                    continue;

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

        // PROPAGA exceção (sem try/catch): o worker traduz em retry/falha. Múltiplos handlers
        // duráveis do mesmo evento rodam na mesma transação do worker (atomicidade do efeito).
        foreach (var handler in handlers)
            await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
