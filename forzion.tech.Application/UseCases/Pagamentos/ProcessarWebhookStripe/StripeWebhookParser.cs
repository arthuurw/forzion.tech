using System.Text.Json.Nodes;

namespace forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

public record StripeWebhookEvento(
    string Type,
    string? PaymentIntentId,
    string? AccountId,
    bool ChargesEnabled,
    long? AmountRefundedCents = null,
    string? MotivoDisputa = null,
    string? TipoMetadata = null);

public static class StripeWebhookParser
{
    public static StripeWebhookEvento Parse(string payload)
    {
        var root = JsonNode.Parse(payload)
            ?? throw new InvalidOperationException("Payload do webhook inválido.");

        var type = root["type"]?.GetValue<string>() ?? string.Empty;
        var data = root["data"]?["object"];

        // charge.refunded: data.object é Charge — `payment_intent` aponta pro PI subjacente.
        // charge.dispute.created: data.object é Dispute — também traz `payment_intent` direto.
        // Outros payment_intent.* eventos: data.object.id já é o próprio PI.
        var paymentIntentId = type switch
        {
            "charge.refunded" => data?["payment_intent"]?.GetValue<string>(),
            "charge.dispute.created" => data?["payment_intent"]?.GetValue<string>(),
            _ when type.StartsWith("payment_intent.", StringComparison.Ordinal) => data?["id"]?.GetValue<string>(),
            _ => null,
        };

        // Eventos Connect (payment_intent.* + account.updated + charge.*) trazem `account` no root.
        // Extrair sempre que presente — handler usa pra validar Connect account de origem.
        var accountId = root["account"]?.GetValue<string>();

        var chargesEnabled = type == "account.updated" &&
            (data?["charges_enabled"]?.GetValue<bool>() ?? false);

        // amount_refunded em centavos, presente em charge.refunded.
        // G-PAY-5: usado para distinguir refund total vs parcial — só refund total muda status.
        var amountRefundedCents = type == "charge.refunded"
            ? data?["amount_refunded"]?.GetValue<long>()
            : null;

        // reason vem do Dispute object — valores comuns: "fraudulent", "duplicate",
        // "subscription_canceled", "product_not_received", etc. Exposto pro template
        // de e-mail urgente que vai pro treinador (precisa saber o motivo pra responder).
        var motivoDisputa = type == "charge.dispute.created"
            ? data?["reason"]?.GetValue<string>()
            : null;

        // metadata.tipo distingue pagamento do plano do treinador (direto-plataforma) do fluxo de aluno.
        var tipoMetadata = type.StartsWith("payment_intent.", StringComparison.Ordinal)
            ? data?["metadata"]?["tipo"]?.GetValue<string>()
            : null;

        return new StripeWebhookEvento(type, paymentIntentId, accountId, chargesEnabled, amountRefundedCents, motivoDisputa, tipoMetadata);
    }
}
