using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
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

    private static async Task<JsonElement> LerCorpo(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    // --- Status codes ---

    [Fact]
    public async Task TryHandleAsync_CredenciaisInvalidasException_Retorna401()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new CredenciaisInvalidasException(), default);
        context.Response.StatusCode.Should().Be(401);
    }

    [Theory]
    [InlineData(typeof(AlunoNaoEncontradoException))]
    [InlineData(typeof(TreinadorNaoEncontradoException))]
    [InlineData(typeof(TreinoNaoEncontradoException))]
    [InlineData(typeof(VinculoNaoEncontradoException))]
    [InlineData(typeof(ExercicioNaoEncontradoException))]
    public async Task TryHandleAsync_NaoEncontradoExceptions_Retorna404(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, exception, default);
        context.Response.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData(typeof(AlunoInativoException))]
    [InlineData(typeof(AcessoNegadoException))]
    [InlineData(typeof(EmailNaoVerificadoException))]
    [InlineData(typeof(TreinadorAguardandoAprovacaoException))]
    [InlineData(typeof(TreinadorInativoException))]
    [InlineData(typeof(TreinadorPagamentoPendenteException))]
    public async Task TryHandleAsync_ForbiddenExceptions_Retorna403(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, exception, default);
        context.Response.StatusCode.Should().Be(403);
    }

    [Theory]
    [InlineData(typeof(EmailNaoVerificadoException), "EMAIL_NAO_VERIFICADO")]
    [InlineData(typeof(TreinadorAguardandoAprovacaoException), "TREINADOR_AGUARDANDO_APROVACAO")]
    [InlineData(typeof(TreinadorInativoException), "TREINADOR_INATIVO")]
    [InlineData(typeof(TreinadorPagamentoPendenteException), "TREINADOR_PAGAMENTO_PENDENTE")]
    public async Task TryHandleAsync_ExcecoesComCode_CorpoExpoeCode(Type exceptionType, string codeEsperado)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, exception, default);
        var body = await LerCorpo(context);
        body.GetProperty("code").GetString().Should().Be(codeEsperado);
    }

    [Theory]
    [InlineData(typeof(EmailJaCadastradoException))]
    [InlineData(typeof(AlunoJaVinculadoException))]
    public async Task TryHandleAsync_ConflictExceptions_Retorna409(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, exception, default);
        context.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_DomainException_Retorna422()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new DomainException("erro de domínio"), default);
        context.Response.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task TryHandleAsync_ExcecaoGenerica_Retorna500()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new Exception("erro inesperado"), default);
        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Retorna400()
    {
        var failures = new[] { new ValidationFailure("Nome", "Nome é obrigatório") };
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new ValidationException(failures), default);
        context.Response.StatusCode.Should().Be(400);
    }

    // --- Response body ---

    [Fact]
    public async Task TryHandleAsync_CredenciaisInvalidasException_CorpoContemTitleNaoAutorizado()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new CredenciaisInvalidasException(), default);
        var body = await LerCorpo(context);
        body.GetProperty("title").GetString().Should().Be("Não autorizado");
    }

    [Fact]
    public async Task TryHandleAsync_AlunoNaoEncontradoException_CorpoContemTitleNaoEncontrado()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new AlunoNaoEncontradoException(), default);
        var body = await LerCorpo(context);
        body.GetProperty("title").GetString().Should().Be("Não encontrado");
    }

    [Fact]
    public async Task TryHandleAsync_ExcecaoGenerica_CorpoContemTitleErroInterno()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new Exception("internal"), default);
        var body = await LerCorpo(context);
        body.GetProperty("title").GetString().Should().Be("Erro interno");
    }

    [Fact]
    public async Task TryHandleAsync_ExcecaoGenerica_DetailNaoExpoeMensagemInterna()
    {
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new Exception("mensagem sensível"), default);
        var body = await LerCorpo(context);
        body.GetProperty("detail").GetString().Should().NotContain("mensível");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_CorpoContemErrosDeCampo()
    {
        var failures = new[]
        {
            new ValidationFailure("Nome", "Nome é obrigatório"),
            new ValidationFailure("Email", "Email inválido")
        };
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new ValidationException(failures), default);
        var body = await LerCorpo(context);
        body.GetProperty("errors").GetProperty("nome").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("Nome é obrigatório");
        body.GetProperty("errors").GetProperty("email").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("Email inválido");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_ChavesEmCamelCase()
    {
        var failures = new[] { new ValidationFailure("DataNascimento", "Data inválida") };
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new ValidationException(failures), default);
        var body = await LerCorpo(context);
        body.GetProperty("errors").TryGetProperty("dataNascimento", out _).Should().BeTrue();
    }

    // --- Return value ---

    [Fact]
    public async Task TryHandleAsync_SempreRetornaTrue()
    {
        var context = CriarHttpContext();
        var result = await _handler.TryHandleAsync(context, new Exception(), default);
        result.Should().BeTrue();
    }

    // --- Logging ---

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

    [Theory]
    [InlineData(typeof(AlunoNaoEncontradoException))]
    [InlineData(typeof(TreinadorNaoEncontradoException))]
    [InlineData(typeof(TreinoNaoEncontradoException))]
    [InlineData(typeof(AcessoNegadoException))]
    [InlineData(typeof(DomainException))]
    public async Task TryHandleAsync_Retorna4xx_LogaWarning(Type exceptionType)
    {
        var exception = exceptionType == typeof(DomainException)
            ? new DomainException("msg")
            : (Exception)Activator.CreateInstance(exceptionType)!;

        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, exception, default);

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
    public async Task TryHandleAsync_ValidationException_NaoLogaNada()
    {
        var failures = new[] { new ValidationFailure("Campo", "Erro") };
        var context = CriarHttpContext();
        await _handler.TryHandleAsync(context, new ValidationException(failures), default);

        _logger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    // --- Guard clauses ---

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
