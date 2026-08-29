using System.Diagnostics;
using System.Net;
using FluentAssertions;
using forzion.tech.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;

namespace forzion.tech.Tests.Infrastructure.Health;

// Unit (sem Docker): valida que checks retornam Degraded (nunca Unhealthy) em falha,
// de modo que /health/ready não mata o pod quando Stripe ou Resend estão indisponíveis.

public class StripeHealthCheckTests
{
    private static StripeHealthCheck Criar(string? secretKey = null, TimeProvider? timeProvider = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(secretKey is not null
                ? new Dictionary<string, string?> { ["Stripe:SecretKey"] = secretKey }
                : new Dictionary<string, string?>())
            .Build();
        return new StripeHealthCheck(config, timeProvider ?? TimeProvider.System);
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

    // Stripe.net não é mockável (comentário da classe acima) — a única forma observável de provar
    // que a 2ª chamada não refez a requisição de rede é o tempo: uma chamada real leva o RTT até a
    // Stripe (centenas de ms), o cache é comparação em memória (sub-ms). Janela ampla o suficiente
    // para não ser flaky mesmo sob carga.
    [Fact]
    public async Task CheckHealthAsync_SegundaChamadaDentroDaJanela_RetornaMemoizadoSemNovaChamadaDeRede()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var check = Criar(secretKey: "sk_test_invalid_key_for_unit_test", time);

        var primeira = await check.CheckHealthAsync(FakeContext());
        var cronometro = Stopwatch.StartNew();
        var segunda = await check.CheckHealthAsync(FakeContext());
        cronometro.Stop();

        segunda.Status.Should().Be(primeira.Status);
        segunda.Description.Should().Be(primeira.Description);
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }
}

public class ResendHealthCheckTests
{
    private static readonly HealthCheckContext FakeCtx = new()
    {
        Registration = new HealthCheckRegistration("resend", _ => null!, null, null)
    };

    private static IHostEnvironment Env(string name) =>
        Mock.Of<IHostEnvironment>(e => e.EnvironmentName == name);

    private static (ResendHealthCheck check, Mock<HttpMessageHandler> handler) CriarComHandler(
        string? apiKey,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string environment = "Development",
        TimeProvider? timeProvider = null)
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

        return (new ResendHealthCheck(factoryMock.Object, config, Env(environment), timeProvider ?? TimeProvider.System), handlerMock);
    }

    [Fact]
    public async Task CheckHealthAsync_SemChaveConfigurada_RetornaHealthy_SemChamadaHttp()
    {
        var (check, handler) = CriarComHandler(apiKey: null);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("não configurado");
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

        var check = new ResendHealthCheck(factoryMock.Object, config, Env("Development"), TimeProvider.System);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Producao_SemChave_RetornaUnhealthy()
    {
        var (check, handler) = CriarComHandler(apiKey: null, environment: "Production");

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_Producao_ChaveConfigurada401_RetornaDegraded_NaoUnhealthy()
    {
        var (check, _) = CriarComHandler(
            apiKey: "re_live_key", statusCode: HttpStatusCode.Unauthorized, environment: "Production");

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_SegundaChamadaDentroDaJanela_NaoRefazRequisicaoHttp()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (check, handler) = CriarComHandler(apiKey: "re_test_key", timeProvider: time);

        await check.CheckHealthAsync(FakeCtx);
        time.Advance(TimeSpan.FromSeconds(29));
        var segunda = await check.CheckHealthAsync(FakeCtx);

        segunda.Status.Should().Be(HealthStatus.Healthy);
        handler.Protected().Verify(
            "SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_ChamadaAposJanelaExpirar_RefazRequisicaoHttp()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (check, handler) = CriarComHandler(apiKey: "re_test_key", timeProvider: time);

        await check.CheckHealthAsync(FakeCtx);
        time.Advance(TimeSpan.FromSeconds(31));
        await check.CheckHealthAsync(FakeCtx);

        handler.Protected().Verify(
            "SendAsync", Times.Exactly(2), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}
