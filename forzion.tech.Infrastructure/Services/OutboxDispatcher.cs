using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Outbox;

namespace forzion.tech.Infrastructure.Services;

// Roteia uma linha outbox para seu executor pelo prefixo do tipo:
//   evt:<FullName> → re-dispatch dos handlers DURÁVEIS do domain-event (mutação crítica)
//   fx:<nome>      → IOutboxEfeitoHandler correspondente (efeito externo, ex.: Stripe)
public sealed class OutboxDispatcher(
    IDomainEventDispatcher eventDispatcher,
    OutboxDurabilityRegistry outboxDurabilidade,
    IEnumerable<IOutboxEfeitoHandler> efeitoHandlers)
{
    public async Task DespacharAsync(OutboxEfeito efeito, CancellationToken cancellationToken = default)
    {
        if (efeito.Tipo.StartsWith("evt:", StringComparison.Ordinal))
        {
            var fullName = efeito.Tipo["evt:".Length..];
            var tipoEvento = outboxDurabilidade.ResolverTipoEvento(fullName)
                ?? throw new InvalidOperationException($"Tipo de evento durável desconhecido: {fullName}.");

            var evento = (IDomainEvent)JsonSerializer.Deserialize(efeito.Payload, tipoEvento)!;
            await eventDispatcher.DispatchDuravelAsync(evento, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (efeito.Tipo.StartsWith("fx:", StringComparison.Ordinal))
        {
            var handler = efeitoHandlers.FirstOrDefault(h => h.Tipo == efeito.Tipo)
                ?? throw new InvalidOperationException($"Nenhum IOutboxEfeitoHandler para o tipo {efeito.Tipo}.");
            await handler.ExecutarAsync(efeito.Payload, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException($"Prefixo de tipo outbox desconhecido: {efeito.Tipo}.");
    }
}
