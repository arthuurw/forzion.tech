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

    /// <summary>
    /// Lists all delivery logs for the given recipient email (LGPD export).
    /// </summary>
    Task<IReadOnlyList<EmailDeliveryLog>> ListarPorEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrubs the recipient e-mail on all logs matching <paramref name="email"/>
    /// (LGPD anonymization). Replaces RecipientEmail with an anonymized placeholder.
    /// </summary>
    Task AnonimizarPorEmailAsync(string email, CancellationToken cancellationToken = default);
}
