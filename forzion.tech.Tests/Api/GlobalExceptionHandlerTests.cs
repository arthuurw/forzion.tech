using FluentAssertions;
using forzion.tech.Api.Middleware;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace forzion.tech.Tests.Api;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _logger = new();
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _handler = new GlobalExceptionHandler(_logger.Object);
    }

    private static HttpContext CriarHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<int> ObterStatusCode(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("status").GetInt32();
    }

    [Fact]
    public async Task TryHandleAsync_DomainException_Retorna422()
    {
        var context = CriarHttpContext();
        var result = await _handler.TryHandleAsync(context, new DomainException("erro de domínio"), default);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task TryHandleAsync_ExcecaoGenerica_Retorna500()
    {
        var context = CriarHttpContext();
        var result = await _handler.TryHandleAsync(context, new Exception("erro inesperado"), default);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TryHandleAsync_Retorna500_LogaError()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new Exception("boom"), default);

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_Retorna4xx_LogaWarning()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new AlunoNaoEncontradoException(), default);

        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_HttpContextNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.TryHandleAsync(null!, new Exception(), default);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TryHandleAsync_ExcecaoNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.TryHandleAsync(CriarHttpContext(), null!, default);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
