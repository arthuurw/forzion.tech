using System.Text.Json.Nodes;

namespace forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

public record StripeWebhookEvento(
    string Type,
    string? PaymentIntentId,
    string? AccountId,
    bool ChargesEnabled,
    long? AmountRefundedCents = null,
    string? MotivoDisputa = null,
    string? TipoMetadata = null,
    string? DisputeId = null);

public static class StripeWebhookParser
{
    public static StripeWebhookEvento Parse(string payload)
    {
        var root = JsonNode.Parse(payload)
            ?? throw new InvalidOperationException("Payload do webhook inválido.");

        var type = TryGetStringValue(root["type"]) ?? string.Empty;
        var data = root["data"]?["object"];

        // charge.refunded: data.object é Charge — `payment_intent` aponta pro PI subjacente.
        // charge.dispute.created: data.object é Dispute — também traz `payment_intent` direto.
        // Stripe pode expandir `payment_intent` como objeto; TryGetStringValue retorna null
        // nesses casos em vez de lançar, evitando poison-retry (400 em loop).
        var paymentIntentId = type switch
        {
            "charge.refunded" => TryGetStringValue(data?["payment_intent"]),
            "charge.dispute.created" => TryGetStringValue(data?["payment_intent"]),
            _ when type.StartsWith("payment_intent.", StringComparison.Ordinal) => TryGetStringValue(data?["id"]),
            _ => null,
        };

        var accountId = TryGetStringValue(root["account"]);

        var chargesEnabled = type == "account.updated" &&
            (TryGetBoolValue(data?["charges_enabled"]) ?? false);

        // G-PAY-5: distingue refund total vs parcial — só refund total muda status.
        var amountRefundedCents = type == "charge.refunded"
            ? TryGetLongValue(data?["amount_refunded"])
            : null;

        // reason vem do Dispute object. Valores comuns: "fraudulent", "duplicate",
        // "subscription_canceled", "product_not_received". Exposto pro template de e-mail
        // urgente que vai pro treinador (precisa do motivo pra responder no Stripe Dashboard).
        var motivoDisputa = type == "charge.dispute.created"
            ? TryGetStringValue(data?["reason"])
            : null;

        var tipoMetadata = type.StartsWith("payment_intent.", StringComparison.Ordinal)
            ? TryGetStringValue(data?["metadata"]?["tipo"])
            : null;

        // charge.dispute.created: data.object é o Dispute — data.object.id é o disputeId que a
        // Dispute.Update API exige para anexar evidências (R9). Distinto do payment_intent.
        var disputeId = type == "charge.dispute.created"
            ? TryGetStringValue(data?["id"])
            : null;

        return new StripeWebhookEvento(type, paymentIntentId, accountId, chargesEnabled, amountRefundedCents, motivoDisputa, tipoMetadata, disputeId);
    }

    private static string? TryGetStringValue(JsonNode? node)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var s))
            return s;
        return null;
    }

    private static long? TryGetLongValue(JsonNode? node)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<long>(out var l))
            return l;
        return null;
    }

    private static bool? TryGetBoolValue(JsonNode? node)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var b))
            return b;
        return null;
    }
}
