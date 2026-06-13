using System.Net;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class ResendEmailServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new(MockBehavior.Strict);
    private readonly Mock<ILogger<ResendEmailService>> _logger = new();

    // Lê o body no callback: ResendEmailService usa `using var request`, então o
    // HttpContent é descartado quando EnviarAsync retorna (não dá pra ler depois).
    private (ResendEmailService service, Func<string?> getBody) Build(EmailSettings settings)
    {
        string? capturedBody = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(
                (req, ct) => capturedBody = req.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

        var httpClient = new HttpClient(_handlerMock.Object);
        var service = new ResendEmailService(httpClient, "re_test_key", "https://api.resend.com/emails", settings, _logger.Object);
        return (service, () => capturedBody);
    }

    private static string FromDoPayload(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("from").GetString()!;

    private static bool TemReplyTo(string json, out string? valor)
    {
        var existe = JsonDocument.Parse(json).RootElement.TryGetProperty("reply_to", out var prop);
        valor = existe ? prop.GetString() : null;
        return existe;
    }

    [Fact]
    public async Task EnviarAsync_UsaRemetenteDeEmailSettings()
    {
        var (service, getBody) = Build(new EmailSettings
        {
            FromName = "forzion.tech [HOMOLOG]",
            FromAddress = "homolog@forzion.tech"
        });

        await service.EnviarAsync("dest@forzion.tech", "Assunto", "<p>corpo</p>");

        FromDoPayload(getBody()!).Should().Be("forzion.tech [HOMOLOG] <homolog@forzion.tech>");
    }

    [Fact]
    public async Task EnviarAsync_RemetenteVazio_UsaFallbackSeguro()
    {
        var (service, getBody) = Build(new EmailSettings
        {
            FromName = "",
            FromAddress = ""
        });

        await service.EnviarAsync("dest@forzion.tech", "Assunto", "<p>corpo</p>");

        FromDoPayload(getBody()!).Should().Be("forzion.tech <noreply@forzion.tech>");
    }

    [Fact]
    public async Task EnviarAsync_ComReplyTo_IncluiCampoNoPayload()
    {
        var (service, getBody) = Build(new EmailSettings());

        await service.EnviarAsync("suporte@forzion.tech", "Assunto", "<p>corpo</p>", replyTo: "usuario@forzion.tech");

        TemReplyTo(getBody()!, out var valor).Should().BeTrue();
        valor.Should().Be("usuario@forzion.tech");
    }

    [Fact]
    public async Task EnviarAsync_SemReplyTo_OmiteCampo()
    {
        var (service, getBody) = Build(new EmailSettings());

        await service.EnviarAsync("dest@forzion.tech", "Assunto", "<p>corpo</p>");

        TemReplyTo(getBody()!, out _).Should().BeFalse();
    }
}
