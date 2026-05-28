using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IEmailDeliveryLogRepository
{
    Task AdicionarAsync(EmailDeliveryLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> ContarPorEventoDesdeAsync(DateTime desde, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check de idempotência para webhooks Resend: dois eventos com o mesmo
    /// <paramref name="resendMessageId"/> e <paramref name="eventType"/> são
    /// re-entregas (Resend faz at-least-once). Use antes de Adicionar para
    /// evitar logs duplicados.
    /// </summary>
    Task<bool> ExisteAsync(string resendMessageId, string eventType, CancellationToken cancellationToken = default);
}
