using FluentAssertions;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

namespace forzion.tech.Tests.Application.Pagamentos;

public class StripeWebhookParserTests
{
    [Fact]
    public void Parse_PayloadNull_LancaInvalidOperationException()
    {
        var act = () => StripeWebhookParser.Parse("null");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_SemCampoType_RetornaTypeVazioECamposNulos()
    {
        // covers: root["type"] null → ?.GetValue null → ?? string.Empty
        // also covers: root["data"] null path
        var result = StripeWebhookParser.Parse("{}");

        result.Type.Should().Be(string.Empty);
        result.PaymentIntentId.Should().BeNull();
        result.AccountId.Should().BeNull();
        result.ChargesEnabled.Should().BeFalse();
    }

    [Fact]
    public void Parse_PaymentIntentSemData_RetornaPaymentIntentIdNulo()
    {
        // covers: data null when StartsWith("payment_intent.") is true
        var result = StripeWebhookParser.Parse("""{"type":"payment_intent.succeeded"}""");

        result.Type.Should().Be("payment_intent.succeeded");
        result.PaymentIntentId.Should().BeNull();
    }

    [Fact]
    public void Parse_PaymentIntentSemIdNoObjeto_RetornaPaymentIntentIdNulo()
    {
        // covers: data["id"] null path (data not null, but "id" key missing)
        var result = StripeWebhookParser.Parse("""{"type":"payment_intent.succeeded","data":{"object":{}}}""");

        result.PaymentIntentId.Should().BeNull();
    }

    [Fact]
    public void Parse_AccountUpdatedSemChaveAccount_RetornaAccountIdNulo()
    {
        // covers: root["account"]?.GetValue null branch
        var result = StripeWebhookParser.Parse(
            """{"type":"account.updated","data":{"object":{"charges_enabled":true}}}""");

        result.AccountId.Should().BeNull();
        result.ChargesEnabled.Should().BeTrue();
    }

    [Fact]
    public void Parse_AccountUpdatedSemChargesEnabled_RetornaFalseViaNullCoalescing()
    {
        // covers: data?["charges_enabled"]?.GetValue null → ?? false
        var result = StripeWebhookParser.Parse(
            """{"type":"account.updated","account":"acct_x","data":{"object":{}}}""");

        result.AccountId.Should().Be("acct_x");
        result.ChargesEnabled.Should().BeFalse();
    }

    [Fact]
    public void Parse_ChargeRefunded_ExtraiPaymentIntentIdEAmountRefunded()
    {
        // charge.refunded: data.object é Charge — payment_intent string aponta pro PI.
        var payload = """
            {
                "type": "charge.refunded",
                "data": {
                    "object": {
                        "id": "ch_123",
                        "payment_intent": "pi_refunded",
                        "amount_refunded": 14990,
                        "refunded": true
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.Type.Should().Be("charge.refunded");
        result.PaymentIntentId.Should().Be("pi_refunded");
        result.AmountRefundedCents.Should().Be(14990L);
    }

    [Fact]
    public void Parse_ChargeRefundedSemPaymentIntent_RetornaPaymentIntentIdNulo()
    {
        // Refund de charge sem PI — não bate no nosso fluxo (sempre criamos via PI).
        var result = StripeWebhookParser.Parse(
            """{"type":"charge.refunded","data":{"object":{"id":"ch_x","amount_refunded":100}}}""");

        result.Type.Should().Be("charge.refunded");
        result.PaymentIntentId.Should().BeNull();
        result.AmountRefundedCents.Should().Be(100L);
    }

    [Fact]
    public void Parse_PaymentIntentSucceeded_AmountRefundedNulo()
    {
        // Garantir que o campo só popula em charge.refunded.
        var result = StripeWebhookParser.Parse(
            """{"type":"payment_intent.succeeded","data":{"object":{"id":"pi_x"}}}""");

        result.AmountRefundedCents.Should().BeNull();
    }

    [Fact]
    public void Parse_ChargeDisputeCreated_ExtraiPaymentIntentIdEMotivo()
    {
        // charge.dispute.created: data.object é Dispute. payment_intent + reason são os campos
        // que o handler precisa pra marcar Pagamento em disputa e notificar treinador.
        var payload = """
            {
                "type": "charge.dispute.created",
                "data": {
                    "object": {
                        "id": "dp_123",
                        "payment_intent": "pi_disputed",
                        "charge": "ch_abc",
                        "amount": 14990,
                        "reason": "fraudulent"
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.Type.Should().Be("charge.dispute.created");
        result.PaymentIntentId.Should().Be("pi_disputed");
        result.MotivoDisputa.Should().Be("fraudulent");
    }

    [Fact]
    public void Parse_ChargeDisputeCreated_SemReason_RetornaMotivoNulo()
    {
        // reason ausente — handler normaliza pra "unknown" via Pagamento.MarcarEmDisputa.
        var result = StripeWebhookParser.Parse(
            """{"type":"charge.dispute.created","data":{"object":{"payment_intent":"pi_y"}}}""");

        result.Type.Should().Be("charge.dispute.created");
        result.PaymentIntentId.Should().Be("pi_y");
        result.MotivoDisputa.Should().BeNull();
    }

    [Fact]
    public void Parse_ChargeDisputeCreated_SemPaymentIntent_RetornaPaymentIntentIdNulo()
    {
        var result = StripeWebhookParser.Parse(
            """{"type":"charge.dispute.created","data":{"object":{"id":"dp_x","reason":"duplicate"}}}""");

        result.PaymentIntentId.Should().BeNull();
        result.MotivoDisputa.Should().Be("duplicate");
    }

    [Fact]
    public void Parse_PaymentIntentSucceeded_MotivoDisputaNulo()
    {
        // Campo só popula em charge.dispute.created — não vaza pra outros tipos.
        var result = StripeWebhookParser.Parse(
            """{"type":"payment_intent.succeeded","data":{"object":{"id":"pi_x"}}}""");

        result.MotivoDisputa.Should().BeNull();
    }

    [Fact]
    public void Parse_ChargeRefunded_PaymentIntentExpandidoComoObjeto_NaoLanca()
    {
        // Stripe expande payment_intent como objeto quando o payload vem de integrações
        // com expand[]=payment_intent. GetValue<string>() em JsonObject lança — poison-retry.
        var payload = """
            {
                "type": "charge.refunded",
                "data": {
                    "object": {
                        "id": "ch_123",
                        "payment_intent": { "id": "pi_expanded", "object": "payment_intent" },
                        "amount_refunded": 14990
                    }
                }
            }
            """;

        var act = () => StripeWebhookParser.Parse(payload);
        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_ChargeRefunded_PaymentIntentExpandidoComoObjeto_RetornaPaymentIntentIdNulo()
    {
        var payload = """
            {
                "type": "charge.refunded",
                "data": {
                    "object": {
                        "id": "ch_123",
                        "payment_intent": { "id": "pi_expanded", "object": "payment_intent" },
                        "amount_refunded": 14990
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.PaymentIntentId.Should().BeNull();
        result.AmountRefundedCents.Should().Be(14990L);
    }

    [Fact]
    public void Parse_ChargeDisputeCreated_PaymentIntentExpandidoComoObjeto_NaoLanca()
    {
        var payload = """
            {
                "type": "charge.dispute.created",
                "data": {
                    "object": {
                        "id": "dp_123",
                        "payment_intent": { "id": "pi_expanded", "object": "payment_intent" },
                        "reason": "fraudulent"
                    }
                }
            }
            """;

        var act = () => StripeWebhookParser.Parse(payload);
        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_ChargeDisputeCreated_PaymentIntentExpandidoComoObjeto_RetornaPaymentIntentIdNulo()
    {
        var payload = """
            {
                "type": "charge.dispute.created",
                "data": {
                    "object": {
                        "id": "dp_123",
                        "payment_intent": { "id": "pi_expanded", "object": "payment_intent" },
                        "reason": "fraudulent"
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.PaymentIntentId.Should().BeNull();
        result.MotivoDisputa.Should().Be("fraudulent");
    }

    [Fact]
    public void Parse_ChargeRefunded_AmountRefundedTipoErrado_NaoLanca()
    {
        var payload = """
            {
                "type": "charge.refunded",
                "data": {
                    "object": {
                        "payment_intent": "pi_abc",
                        "amount_refunded": "nao_um_numero"
                    }
                }
            }
            """;

        var act = () => StripeWebhookParser.Parse(payload);
        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_ChargeRefunded_AmountRefundedTipoErrado_RetornaAmountNulo()
    {
        var payload = """
            {
                "type": "charge.refunded",
                "data": {
                    "object": {
                        "payment_intent": "pi_abc",
                        "amount_refunded": "nao_um_numero"
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.AmountRefundedCents.Should().BeNull();
        result.PaymentIntentId.Should().Be("pi_abc");
    }

    [Fact]
    public void Parse_ChargeDisputeCreated_ReasonComoObjeto_NaoLanca()
    {
        var payload = """
            {
                "type": "charge.dispute.created",
                "data": {
                    "object": {
                        "payment_intent": "pi_abc",
                        "reason": { "code": "fraudulent" }
                    }
                }
            }
            """;

        var act = () => StripeWebhookParser.Parse(payload);
        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_ChargeDisputeCreated_ReasonComoObjeto_RetornaMotivoNulo()
    {
        var payload = """
            {
                "type": "charge.dispute.created",
                "data": {
                    "object": {
                        "payment_intent": "pi_abc",
                        "reason": { "code": "fraudulent" }
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.MotivoDisputa.Should().BeNull();
    }

    [Fact]
    public void Parse_HappyPath_ChargeRefunded_ValoresCorretos()
    {
        var payload = """
            {
                "type": "charge.refunded",
                "account": "acct_123",
                "data": {
                    "object": {
                        "id": "ch_abc",
                        "payment_intent": "pi_abc123",
                        "amount_refunded": 9900,
                        "refunded": true
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.Type.Should().Be("charge.refunded");
        result.PaymentIntentId.Should().Be("pi_abc123");
        result.AmountRefundedCents.Should().Be(9900L);
        result.AccountId.Should().Be("acct_123");
    }

    [Fact]
    public void Parse_HappyPath_ChargeDisputeCreated_ValoresCorretos()
    {
        var payload = """
            {
                "type": "charge.dispute.created",
                "data": {
                    "object": {
                        "id": "dp_abc",
                        "payment_intent": "pi_disputed123",
                        "reason": "subscription_canceled"
                    }
                }
            }
            """;

        var result = StripeWebhookParser.Parse(payload);

        result.Type.Should().Be("charge.dispute.created");
        result.PaymentIntentId.Should().Be("pi_disputed123");
        result.MotivoDisputa.Should().Be("subscription_canceled");
    }
}
