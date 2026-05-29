using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IWhatsAppDeliveryLogRepository
{
    Task AdicionarAsync(WhatsAppDeliveryLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check de idempotência para webhooks Meta: dois eventos com o mesmo
    /// <paramref name="metaMessageId"/> e <paramref name="eventType"/> são
    /// re-entregas (Meta entrega at-least-once). Use antes de Adicionar para
    /// evitar logs duplicados.
    /// </summary>
    Task<bool> ExisteAsync(string metaMessageId, string eventType, CancellationToken cancellationToken = default);
}
