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

    private static StripeService CriarServico(string webhookSecret = WebhookSecret)
    {
        var settings = Options.Create(new StripeSettings { WebhookSecret = webhookSecret });
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
    public async Task ValidarWebhookAsync_AssinaturaValida_RetornaTrue()
    {
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow);

        var resultado = await CriarServico().ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().BeTrue();
    }

    [Fact]
    public async Task ValidarWebhookAsync_PayloadAdulterado_RetornaFalse()
    {
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow);
        var payloadAdulterado =
            """{"id":"evt_1","object":"event","type":"payment_intent.succeeded","data":{"object":{"tampered":true}}}""";

        var resultado = await CriarServico().ValidarWebhookAsync(payloadAdulterado, assinatura);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task ValidarWebhookAsync_SecretErrado_RetornaFalse()
    {
        var assinatura = AssinarStripe(Payload, "whsec_secret_diferente", DateTimeOffset.UtcNow);

        var resultado = await CriarServico().ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task ValidarWebhookAsync_TimestampForaDaTolerancia_RetornaFalse()
    {
        var assinatura = AssinarStripe(Payload, WebhookSecret, DateTimeOffset.UtcNow.AddHours(-1));

        var resultado = await CriarServico().ValidarWebhookAsync(Payload, assinatura);

        resultado.Should().BeFalse();
    }
}
