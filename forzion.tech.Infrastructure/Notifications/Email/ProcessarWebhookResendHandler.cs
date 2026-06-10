using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Svix;

namespace forzion.tech.Infrastructure.Notifications.Email;

public record ProcessarWebhookResendCommand(
    string Payload,
    string SvixId,
    string SvixTimestamp,
    string SvixSignature);

public class ProcessarWebhookResendHandler(
    IEmailDeliveryLogRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ProcessarWebhookResendHandler> logger)
{
    private static readonly HashSet<string> EventosRelevantes = new(StringComparer.OrdinalIgnoreCase)
    {
        "email.delivered",
        "email.bounced",
        "email.complained",
        "email.spam_complaint"
    };

    public virtual async Task<Result> HandleAsync(
        ProcessarWebhookResendCommand command,
        string webhookSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            logger.LogWarning("ProcessarWebhookResendHandler: Resend:WebhookSecret não configurado.");
            return Result.Failure(Error.Business("Webhook não configurado."));
        }

        if (!VerificarAssinatura(command, webhookSecret))
            return Result.Failure(Error.Business("Assinatura do webhook inválida."));

        var parsed = ParsePayload(command.Payload);
        if (parsed is null)
        {
            logger.LogWarning("ProcessarWebhookResendHandler: payload inválido.");
            return Result.Failure(Error.Business("Payload inválido."));
        }

        if (!EventosRelevantes.Contains(parsed.EventType))
        {
            logger.LogDebug("Evento Resend ignorado: {EventType}.", parsed.EventType);
            return Result.Success();
        }

        // Idempotência: Resend entrega at-least-once. (ResendMessageId, EventType)
        // identifica unicamente um evento; se já existe, é re-entrega → no-op silencioso.
        if (await logRepository.ExisteAsync(parsed.EmailId, parsed.EventType, cancellationToken).ConfigureAwait(false))
        {
            logger.LogDebug("Evento Resend já processado (messageId: {MessageId}, type: {EventType}). Ignorando re-entrega.",
                parsed.EmailId, parsed.EventType);
            return Result.Success();
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var log = EmailDeliveryLog.Criar(
            parsed.EmailId,
            parsed.EventType,
            parsed.RecipientEmail,
            parsed.CreatedAt,
            command.Payload,
            agora);

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        // O ExisteAsync acima é só fast-path: entre o pré-check e o commit duas entregas
        // concorrentes do MESMO evento (Resend é at-least-once) podem ambas passar e colidir
        // no índice único (resend_message_id, event_type) → 23505. Isso significa já-processado:
        // log Debug + segue, sem 500. Só 23505 é engolido; outras DbUpdateException propagam.
        catch (DbUpdateException ex) when ((ex.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            logger.LogDebug(ex,
                "Evento Resend já processado (race de re-entrega; messageId: {MessageId}, type: {EventType}). Ignorando.",
                parsed.EmailId, parsed.EventType);
            return Result.Success();
        }

        logger.LogInformation(
            "Evento Resend registrado: {EventType} para {Email} (messageId: {MessageId}).",
            parsed.EventType, parsed.RecipientEmail, parsed.EmailId);

        return Result.Success();
    }

    private static bool VerificarAssinatura(ProcessarWebhookResendCommand command, string secret)
    {
        try
        {
            var headers = new System.Net.WebHeaderCollection
            {
                ["svix-id"] = command.SvixId,
                ["svix-timestamp"] = command.SvixTimestamp,
                ["svix-signature"] = command.SvixSignature
            };

            new Webhook(secret).Verify(command.Payload, headers);
            return true;
        }
        // Svix lança WebhookVerificationException; o tipo concreto não é público de forma
        // estável entre versões do pacote, então casamos por nome (assinatura inválida/erro
        // de verificação) — qualquer falha de Verify significa assinatura inválida → false.
        catch (Exception ex) when (ex.GetType().Name.Contains("Verification", StringComparison.OrdinalIgnoreCase)
                                   || ex.GetType().Name.Contains("Webhook", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    private static ResendEventData? ParsePayload(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventType = root.GetProperty("type").GetString() ?? string.Empty;

            if (!root.TryGetProperty("data", out var data))
                return null;

            var emailId = data.TryGetProperty("email_id", out var emailIdProp)
                ? emailIdProp.GetString() ?? string.Empty
                : string.Empty;

            var recipientEmail = string.Empty;
            if (data.TryGetProperty("to", out var toArr) && toArr.GetArrayLength() > 0)
                recipientEmail = toArr[0].GetString() ?? string.Empty;

            var createdAt = root.TryGetProperty("created_at", out var caProp)
                ? caProp.GetDateTime()
                : DateTime.UtcNow;

            return new ResendEventData(eventType, emailId, recipientEmail, createdAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ResendEventData(string EventType, string EmailId, string RecipientEmail, DateTime CreatedAt);
}
