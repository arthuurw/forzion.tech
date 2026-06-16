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

    /// <summary>
    /// Lists all delivery logs for the given recipient phone (LGPD export).
    /// </summary>
    Task<IReadOnlyList<WhatsAppDeliveryLog>> ListarPorTelefoneAsync(string telefone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrubs the recipient hash on all logs matching <paramref name="telefone"/>
    /// (LGPD anonymization). Replaces RecipientPhoneHash with an anonymized placeholder.
    /// </summary>
    Task AnonimizarPorTelefoneAsync(string telefone, CancellationToken cancellationToken = default);
}
