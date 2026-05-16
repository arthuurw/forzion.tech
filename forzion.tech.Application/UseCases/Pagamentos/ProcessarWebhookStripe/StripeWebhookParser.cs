using System.Text.Json;
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

        var paymentIntentId = type.StartsWith("payment_intent.")
            ? data?["id"]?.GetValue<string>()
            : null;

        var accountId = type == "account.updated"
            ? root["account"]?.GetValue<string>()
            : null;

        var chargesEnabled = type == "account.updated" &&
            (data?["charges_enabled"]?.GetValue<bool>() ?? false);

        return new StripeWebhookEvento(type, paymentIntentId, accountId, chargesEnabled);
    }
}
