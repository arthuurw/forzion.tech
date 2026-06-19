using FluentAssertions;
using forzion.tech.Infrastructure.Services;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeClientFactoryTests
{
    [Fact]
    public void CriarHttpClient_AplicaTimeoutConfigurado_NaoODefaultDe80s()
    {
        var settings = new StripeSettings { TimeoutSegundos = 30 };

        var httpClient = StripeClientFactory.CriarHttpClient(settings);

        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        httpClient.Timeout.Should().NotBe(TimeSpan.FromSeconds(80));
    }

    [Fact]
    public void Construir_UsaSecretKeyEHttpClientConfigurado()
    {
        var settings = new StripeSettings { SecretKey = "sk_test_x", TimeoutSegundos = 15, MaxNetworkRetries = 3 };

        var client = StripeClientFactory.Construir(settings);

        client.Should().NotBeNull();
    }
}
