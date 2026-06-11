using System.Net;
using FluentAssertions;
using forzion.tech.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Moq.Protected;

namespace forzion.tech.Tests.Infrastructure.Health;

// Unit (sem Docker): valida que checks retornam Degraded (nunca Unhealthy) em falha,
// de modo que /health/ready não mata o pod quando Stripe ou Resend estão indisponíveis.

public class StripeHealthCheckTests
{
    private static StripeHealthCheck Criar(string? secretKey = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(secretKey is not null
                ? new Dictionary<string, string?> { ["Stripe:SecretKey"] = secretKey }
                : new Dictionary<string, string?>())
            .Build();
        return new StripeHealthCheck(config);
    }

    private static HealthCheckContext FakeContext() => new()
    {
        Registration = new HealthCheckRegistration("stripe", _ => null!, null, null)
    };

    [Fact]
    public async Task CheckHealthAsync_SemChaveConfigurada_RetornaHealthy()
    {
        var check = Criar(secretKey: null);

        var result = await check.CheckHealthAsync(FakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("não configurado");
    }

    [Fact]
    public async Task CheckHealthAsync_ChaveVazia_RetornaHealthy()
    {
        var check = Criar(secretKey: "");

        var result = await check.CheckHealthAsync(FakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_FalhaDeRede_RetornaDegraded_NaoUnhealthy()
    {
        // Stripe.net SDK não é mockável sem subclasse; chave inválida faz o SDK emitir
        // StripeException (com rede) ou HttpRequestException (sem rede em CI).
        // Ambos os caminhos devem mapear para Degraded — o invariante a testar.
        var check = Criar(secretKey: "sk_test_invalid_key_for_unit_test");

        var result = await check.CheckHealthAsync(FakeContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy);
    }
}

public class ResendHealthCheckTests
{
    private static readonly HealthCheckContext FakeCtx = new()
    {
        Registration = new HealthCheckRegistration("resend", _ => null!, null, null)
    };

    private static (ResendHealthCheck check, Mock<HttpMessageHandler> handler) CriarComHandler(
        string? apiKey,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient(string.Empty))
            .Returns(new HttpClient(handlerMock.Object));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(apiKey is not null
                ? new Dictionary<string, string?> { ["Resend:ApiKey"] = apiKey }
                : new Dictionary<string, string?>())
            .Build();

        return (new ResendHealthCheck(factoryMock.Object, config), handlerMock);
    }

    [Fact]
    public async Task CheckHealthAsync_SemChaveConfigurada_RetornaHealthy_SemChamadaHttp()
    {
        var (check, handler) = CriarComHandler(apiKey: null);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("não configurado");
        // Nenhuma chamada HTTP deve ter sido feita
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_ResendResponde200_RetornaHealthy()
    {
        var (check, _) = CriarComHandler(apiKey: "re_test_key", statusCode: HttpStatusCode.OK);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ResendResponde401_RetornaDegraded_NaoUnhealthy()
    {
        var (check, _) = CriarComHandler(apiKey: "re_test_key", statusCode: HttpStatusCode.Unauthorized);

        var result = await check.CheckHealthAsync(FakeCtx);

        // 401 = Resend está respondendo mas a chave é inválida → Degraded, não Unhealthy
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ResendResponde500_RetornaDegraded_NaoUnhealthy()
    {
        var (check, _) = CriarComHandler(apiKey: "re_test_key", statusCode: HttpStatusCode.InternalServerError);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_HttpRequestException_RetornaDegraded_NaoUnhealthy()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient(string.Empty))
            .Returns(new HttpClient(handlerMock.Object));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Resend:ApiKey"] = "re_test_key" })
            .Build();

        var check = new ResendHealthCheck(factoryMock.Object, config);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy);
    }
}
