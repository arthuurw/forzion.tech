using System.Net;
using FluentAssertions;
using forzion.tech.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Moq.Protected;

namespace forzion.tech.Tests.Infrastructure.Health;

public class WhatsAppHealthCheckTests
{
    private static readonly HealthCheckContext FakeCtx = new()
    {
        Registration = new HealthCheckRegistration("whatsapp", _ => null!, null, null)
    };

    private static (WhatsAppHealthCheck check, Mock<HttpMessageHandler> handler) Criar(
        IDictionary<string, string?> config, HttpStatusCode statusCode = HttpStatusCode.OK)
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
        factoryMock.Setup(f => f.CreateClient(string.Empty)).Returns(new HttpClient(handlerMock.Object));

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();

        return (new WhatsAppHealthCheck(factoryMock.Object, configuration), handlerMock);
    }

    [Fact]
    public async Task CheckHealthAsync_Desabilitado_ComCredenciais_RetornaHealthy_SemChamadaHttp()
    {
        var (check, handler) = Criar(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "false",
            ["WhatsApp:PhoneNumberId"] = "123456",
            ["WhatsApp:AccessToken"] = "token-valido"
        });

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("desativado temporariamente");
        handler.Protected().Verify(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_DesabilitadoAusente_SemCredenciais_RetornaHealthy_SemChamadaHttp()
    {
        var (check, handler) = Criar(new Dictionary<string, string?>());

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
        handler.Protected().Verify(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_Habilitado_Responde200_RetornaHealthy()
    {
        var (check, _) = Criar(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "true",
            ["WhatsApp:PhoneNumberId"] = "123456",
            ["WhatsApp:AccessToken"] = "token-valido"
        }, HttpStatusCode.OK);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Habilitado_Responde500_RetornaDegraded()
    {
        var (check, _) = Criar(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "true",
            ["WhatsApp:PhoneNumberId"] = "123456",
            ["WhatsApp:AccessToken"] = "token-valido"
        }, HttpStatusCode.InternalServerError);

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_Habilitado_SemCredenciais_RetornaHealthy_SemChamadaHttp()
    {
        var (check, handler) = Criar(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "true"
        });

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("não configurado");
        handler.Protected().Verify(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}
