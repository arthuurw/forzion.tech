using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

public record ProcessarWebhookWhatsAppCommand(string Payload, string Signature);

public class ProcessarWebhookWhatsAppHandler(
    IWhatsAppDeliveryLogRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ProcessarWebhookWhatsAppHandler> logger)
{
    public virtual async Task<Result> HandleAsync(
        ProcessarWebhookWhatsAppCommand command,
        string appSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(appSecret))
        {
            logger.LogWarning("ProcessarWebhookWhatsAppHandler: WhatsApp:AppSecret não configurado.");
            return Result.Failure(Error.Business("webhook_whatsapp.nao_configurado", "Webhook não configurado."));
        }

        if (!VerificarAssinatura(command.Payload, command.Signature, appSecret))
            return Result.Failure(Error.Business("webhook_whatsapp.assinatura_invalida", "Assinatura inválida."));

        var statuses = ParseStatuses(command.Payload);
        if (statuses is null)
        {
            // Payload sem statuses (ex.: mensagem recebida) — ignorar silenciosamente.
            logger.LogDebug("ProcessarWebhookWhatsAppHandler: payload sem statuses — ignorado.");
            return Result.Success();
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var persistiuAlgum = false;

        // Um único batch da Meta pode trazer duas entries idênticas para o mesmo messageId e
        // o mesmo eventType. Dedup intra-batch ANTES de persistir evita dois inserts que
        // colidiriam no índice único — o ExisteAsync não os pega, pois ambos são novos no mesmo
        // SaveChanges, com nada commitado ainda.
        var vistosNoBatch = new HashSet<(string MessageId, string Status)>();

        foreach (var status in statuses)
        {
            if (!vistosNoBatch.Add((status.MessageId, status.Status)))
                continue;

            if (await logRepository.ExisteAsync(status.MessageId, status.Status, cancellationToken).ConfigureAwait(false))
            {
                logger.LogDebug(
                    "Evento WhatsApp já processado (messageId: {MessageId}, status: {Status}). Ignorando re-entrega.",
                    status.MessageId, status.Status);
                continue;
            }

            var log = WhatsAppDeliveryLog.Criar(
                status.MessageId,
                status.Status,
                status.RecipientId,
                status.OcorridoEm,
                command.Payload,
                agora);

            await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
            persistiuAlgum = true;

            logger.LogInformation(
                "Evento WhatsApp registrado: {Status} para {Phone} (messageId: {MessageId}).",
                status.Status, status.RecipientId, status.MessageId);
        }

        if (persistiuAlgum)
        {
            try
            {
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            // ExisteAsync é só fast-path: entre o pré-check e o commit, redeliveries concorrentes
            // da Meta (at-least-once) podem colidir no índice único (meta_message_id, event_type)
            // → 23505. Já-processado: log Debug + segue, sem 500. Só 23505 é engolido; demais propagam.
            catch (DbUpdateException ex) when ((ex.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                logger.LogDebug(ex,
                    "Evento(s) WhatsApp já processado(s) (race de re-entrega). Ignorando.");
                return Result.Success();
            }
        }

        return Result.Success();
    }

    private static bool VerificarAssinatura(string payload, string signature, string secret)
    {
        // Meta envia: X-Hub-Signature-256: sha256=<hexhmac>
        const string prefix = "sha256=";
        if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var receivedHex = signature[prefix.Length..];

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var computedBytes = HMACSHA256.HashData(keyBytes, payloadBytes);
        var computedHex = Convert.ToHexString(computedBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(receivedHex.ToLowerInvariant()));
    }

    /// <summary>
    /// Extrai os status entries do payload Meta. Retorna null se o payload não
    /// contiver o caminho entry[].changes[].value.statuses[] (ex.: mensagem recebida).
    /// </summary>
    private IReadOnlyList<WhatsAppStatusEntry>? ParseStatuses(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("entry", out var entries))
                return null;

            var result = new List<WhatsAppStatusEntry>();

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes))
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                        continue;

                    if (!value.TryGetProperty("statuses", out var statusesEl))
                        continue;

                    foreach (var s in statusesEl.EnumerateArray())
                    {
                        var messageId = s.TryGetProperty("id", out var idProp)
                            ? idProp.GetString() ?? string.Empty
                            : string.Empty;

                        var status = s.TryGetProperty("status", out var statusProp)
                            ? statusProp.GetString() ?? string.Empty
                            : string.Empty;

                        var recipientId = s.TryGetProperty("recipient_id", out var recipProp)
                            ? recipProp.GetString() ?? string.Empty
                            : string.Empty;

                        var ocorridoEm = s.TryGetProperty("timestamp", out var tsProp)
                            && long.TryParse(tsProp.GetString(), out var unix)
                            ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                            : timeProvider.GetUtcNow().UtcDateTime;

                        result.Add(new WhatsAppStatusEntry(messageId, status, recipientId, ocorridoEm));
                    }
                }
            }

            // Se entry[] existe mas nenhum status foi encontrado, ainda é "sem statuses"
            if (result.Count == 0)
                return null;

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record WhatsAppStatusEntry(
        string MessageId,
        string Status,
        string RecipientId,
        DateTime OcorridoEm);
}
