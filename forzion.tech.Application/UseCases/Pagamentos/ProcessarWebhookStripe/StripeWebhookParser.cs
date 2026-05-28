using System.Text.Json.Nodes;

namespace forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

public record StripeWebhookEvento(
    string Type,
    string? PaymentIntentId,
    string? AccountId,
    bool ChargesEnabled);

public static class StripeWebhookParser
{
    public static StripeWebhookEvento Parse(string payload)
    {
        var root = JsonNode.Parse(payload)
            ?? throw new InvalidOperationException("Payload do webhook inválido.");

        var type = root["type"]?.GetValue<string>() ?? string.Empty;
        var data = root["data"]?["object"];

        var paymentIntentId = type.StartsWith("payment_intent.", StringComparison.Ordinal)
            ? data?["id"]?.GetValue<string>()
            : null;

        // Eventos Connect (payment_intent.* + account.updated) trazem `account` no root.
        // Extrair sempre que presente — handler usa pra validar Connect account de origem.
        var accountId = root["account"]?.GetValue<string>();

        var chargesEnabled = type == "account.updated" &&
            (data?["charges_enabled"]?.GetValue<bool>() ?? false);

        return new StripeWebhookEvento(type, paymentIntentId, accountId, chargesEnabled);
    }
}
