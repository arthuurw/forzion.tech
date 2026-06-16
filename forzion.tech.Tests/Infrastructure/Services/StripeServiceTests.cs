using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeServiceTests
{
    private const string WebhookSecret = "whsec_test_secret";

    // api_version casa com o SDK p/ não cair no path de mismatch (real webhook sempre traz o campo).
    private static readonly string Payload =
        "{\"id\":\"evt_1\",\"object\":\"event\",\"api_version\":\""
        + Stripe.StripeConfiguration.ApiVersion
        + "\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{}}}";

    private static StripeService CriarServico(string webhookSecret = WebhookSecret, bool? expectLivemode = null)
    {
        var settings = Options.Create(new StripeSettings { WebhookSecret = webhookSecret, ExpectLivemode = expectLivemode });
        return new StripeService(settings, TimeProvider.System, Mock.Of<ILogger<StripeService>>());
    }

    // Stripe-Signature header: t=<unix>,v1=<lowercase hex HMACSHA256(secret, "<unix>.<payload>")>
    private static string AssinarStripe(string payload, string secret, DateTimeOffset timestamp)
    {
        var t = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signedPayload = $"{t}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var v1 = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={t},v1={v1}";
    }

    [Fact]
    public async Task ValidarWebhookAsync_AssinaturaValida_RetornaEventoVerificado()
    {
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow);

        var resultado = await CriarServico().ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().NotBeNull();
        resultado.Should().Contain("evt_1", "retorna o JSON do evento verificado para o caller parsear");
    }

    [Fact]
    public async Task ValidarWebhookAsync_RetornaPayloadCruVerbatim_PreservandoCamposNoParse()
    {
        var payload =
            "{\"id\":\"evt_meta\",\"object\":\"event\",\"api_version\":\""
            + Stripe.StripeConfiguration.ApiVersion
            + "\",\"type\":\"payment_intent.succeeded\",\"account\":\"acct_xpto\","
            + "\"data\":{\"object\":{\"id\":\"pi_1\",\"object\":\"payment_intent\",\"metadata\":{\"assinatura_id\":\"sub_42\"}}}}";
        var assinatura = AssinarStripe(payload, WebhookSecret, DateTimeOffset.UtcNow);

        var resultado = await CriarServico().ValidarWebhookAsync(payload, assinatura);

        resultado.Should().Be(payload);
        var evento = Stripe.EventUtility.ParseEvent(resultado);
        evento.Account.Should().Be("acct_xpto");
        var pi = (Stripe.PaymentIntent)evento.Data.Object;
        pi.Metadata["assinatura_id"].Should().Be("sub_42");
    }

    [Fact]
    public async Task ValidarWebhookAsync_PayloadAdulterado_RetornaNull()
    {
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow);
        var payloadAdulterado =
            """{"id":"evt_1","object":"event","type":"payment_intent.succeeded","data":{"object":{"tampered":true}}}""";

        var resultado = await CriarServico().ValidarWebhookAsync(payloadAdulterado, assinatura);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ValidarWebhookAsync_SecretErrado_RetornaNull()
    {
        var assinatura = AssinarStripe(Payload, "whsec_secret_diferente", DateTimeOffset.UtcNow);

        var resultado = await CriarServico().ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ValidarWebhookAsync_TimestampForaDaTolerancia_RetornaNull()
    {
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow.AddHours(-1));

        var resultado = await CriarServico().ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ValidarWebhookAsync_LivemodeDivergente_RetornaNull()
    {
        // Payload é test-mode (sem campo livemode ⇒ false); serviço espera live ⇒ rejeita (SEC-03).
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow);

        var resultado = await CriarServico(expectLivemode: true).ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ValidarWebhookAsync_LivemodeCompatível_RetornaEventoVerificado()
    {
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow);

        var resultado = await CriarServico(expectLivemode: false).ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().NotBeNull();
    }
}
