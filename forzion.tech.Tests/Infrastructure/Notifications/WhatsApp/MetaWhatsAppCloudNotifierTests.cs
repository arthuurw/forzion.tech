using System.Net;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

public class MetaWhatsAppCloudNotifierTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new(MockBehavior.Strict);
    private readonly Mock<ILogger<MetaWhatsAppCloudNotifier>> _logger = new();

    private MetaWhatsAppCloudNotifier BuildNotifier(HttpStatusCode status = HttpStatusCode.OK, string? responseBody = null)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody ?? "{}")
            });

        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v21.0/123456789/")
        };

        return new MetaWhatsAppCloudNotifier(httpClient, _logger.Object);
    }

    private HttpRequestMessage? CaptureRequest()
    {
        HttpRequestMessage? captured = null;

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });

        return captured;
    }

    [Fact]
    public async Task SendAsync_HappyPath_PostParaEndpointCorreto()
    {
        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v21.0/123456789/")
        };
        var notifier = new MetaWhatsAppCloudNotifier(httpClient, _logger.Object);

        await notifier.SendAsync("+55 (11) 99999-9999", "Mensagem de teste");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.ToString().Should().EndWith("messages");
    }

    [Fact]
    public async Task SendAsync_HappyPath_PayloadContemCamposObrigatorios()
    {
        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v21.0/123456789/")
        };
        var notifier = new MetaWhatsAppCloudNotifier(httpClient, _logger.Object);

        await notifier.SendAsync("5511999999999", "Olá, aluno aprovado!");

        var json = await captured!.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        doc.RootElement.GetProperty("recipient_type").GetString().Should().Be("individual");
        doc.RootElement.GetProperty("to").GetString().Should().Be("5511999999999");
        doc.RootElement.GetProperty("type").GetString().Should().Be("text");
        doc.RootElement.GetProperty("text").GetProperty("body").GetString().Should().Be("Olá, aluno aprovado!");
    }

    [Theory]
    [InlineData("+55 (11) 99999-9999", "5511999999999")]
    [InlineData("+5511999999999", "5511999999999")]
    [InlineData("55 11 9999-9999", "551199999999")]   // sem parênteses, com espaços e hífen
    [InlineData("5511999999999", "5511999999999")]
    public async Task SendAsync_NormalizaTelefone_RemoveCaracteresEspeciais(string entrada, string esperado)
    {
        HttpRequestMessage? captured = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v21.0/123456789/")
        };
        var notifier = new MetaWhatsAppCloudNotifier(httpClient, _logger.Object);

        await notifier.SendAsync(entrada, "msg");

        var json = await captured!.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("to").GetString().Should().Be(esperado);
    }

    [Fact]
    public async Task SendAsync_RespostaErro_LogaWarningNaoLancaExcecao()
    {
        var notifier = BuildNotifier(HttpStatusCode.BadRequest, """{"error":"invalid number"}""");

        var act = async () => await notifier.SendAsync("5511999999999", "msg");

        await act.Should().NotThrowAsync();
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("BadRequest")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_HttpRequestException_LogaErrorNaoLancaExcecao()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v21.0/123456789/")
        };
        var notifier = new MetaWhatsAppCloudNotifier(httpClient, _logger.Object);

        var act = async () => await notifier.SendAsync("5511999999999", "msg");

        await act.Should().NotThrowAsync();
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<HttpRequestException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_Cancelado_PropagaCancellationException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v21.0/123456789/")
        };
        var notifier = new MetaWhatsAppCloudNotifier(httpClient, _logger.Object);

        var act = async () => await notifier.SendAsync("5511999999999", "msg", cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }
}
