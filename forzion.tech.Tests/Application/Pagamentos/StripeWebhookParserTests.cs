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
}
