using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Stripe;

namespace forzion.tech.Tests.Infrastructure;

public class StripeWebhookApiVersionTests
{
    private const string TrainDosEndpointsCriados = "dahlia";
    private const string SegredoWebhook = "whsec_teste_local";

    [Fact]
    public void ApiVersionDoSdk_DeveFicarNaMesmaTrainDosWebhookEndpointsJaCriados()
    {
        TrainDe(StripeConfiguration.ApiVersion).Should().Be(
            TrainDosEndpointsCriados,
            "o Stripe.net rejeita evento de train diferente e ValidarWebhookAsync engole a StripeException: "
            + "um bump que troca de train derruba a confirmação de pagamento em produção em silêncio "
            + "(só LogWarning) até os endpoints do dashboard serem recriados com a API version nova");
    }

    [Fact]
    public void ConstructEvent_EventoDeTrainDiferenteDoSdk_LancaStripeException()
    {
        var payload = EventoJson("2025-03-31.basil");

        var act = () => EventUtility.ConstructEvent(payload, Assinar(payload), SegredoWebhook);

        act.Should().Throw<StripeException>();
    }

    [Fact]
    public void ConstructEvent_MesmaTrainComDataMaisAntiga_Aceita()
    {
        var payload = EventoJson($"2026-04-22.{TrainDosEndpointsCriados}");

        var evento = EventUtility.ConstructEvent(payload, Assinar(payload), SegredoWebhook);

        evento.Id.Should().Be("evt_teste");
    }

    private static string TrainDe(string apiVersion)
    {
        var separador = apiVersion.IndexOf('.');
        return separador < 0 ? string.Empty : apiVersion[(separador + 1)..];
    }

    private static string EventoJson(string apiVersion) =>
        """
        {"id":"evt_teste","object":"event","api_version":"API_VERSION","created":1,"livemode":true,
         "type":"payment_intent.succeeded","data":{"object":{"id":"pi_teste","object":"payment_intent"}}}
        """.Replace("API_VERSION", apiVersion, StringComparison.Ordinal);

    private static string Assinar(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SegredoWebhook));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
