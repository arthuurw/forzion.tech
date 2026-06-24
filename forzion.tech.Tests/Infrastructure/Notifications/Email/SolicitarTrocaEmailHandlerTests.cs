using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class SolicitarTrocaEmailHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITrocaEmailTokenRepository> _tokenRepo = new();
    private readonly Mock<IEmailCriticoDispatcher> _emailCritico = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<SolicitarTrocaEmailHandler>> _logger = new();
    private readonly SolicitarTrocaEmailHandler _handler;

    private static readonly Guid ContaId = Guid.NewGuid();

    public SolicitarTrocaEmailHandlerTests()
    {
        _handler = new SolicitarTrocaEmailHandler(
            _contaRepo.Object,
            _tokenRepo.Object,
            _emailCritico.Object,
            _unitOfWork.Object,
            _timeProvider,
            _logger.Object,
            new SolicitarTrocaEmailCommandValidator());
    }

    private Conta BuildConta(string email = "atual@test.com") =>
        Conta.Criar(DomainEmail.Criar(email).Value, "hash", TipoConta.Aluno, _timeProvider.GetUtcNow().UtcDateTime).Value;

    [Fact]
    public async Task HandleAsync_NovoEmailIgualAoAtual_NaoCriaTokenNemEnfileira()
    {
        var conta = BuildConta("atual@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "ATUAL@test.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<TrocaEmailToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailCritico.Verify(d => d.Enfileirar(It.IsAny<EmailCriticoTemplate>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NovoEmailEmUsoPorOutraConta_RespostaGenericaSemTokenSemEnfileirar()
    {
        var contaAtual = BuildConta("atual@test.com");
        var outraConta = BuildConta("novo@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(contaAtual);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(outraConta);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "novo@test.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<TrocaEmailToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailCritico.Verify(d => d.Enfileirar(It.IsAny<EmailCriticoTemplate>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NovoEmailDisponivel_CriaTokenHashedComExpiracao30min_EEnfileiraAoNovoEmail()
    {
        var conta = BuildConta("atual@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        TrocaEmailToken? captured = null;
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<TrocaEmailToken>(), It.IsAny<CancellationToken>()))
            .Callback<TrocaEmailToken, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        string? segredo = null;
        _emailCritico.Setup(d => d.Enfileirar(EmailCriticoTemplate.TrocaEmail, "novo@test.com", It.IsAny<string>()))
            .Callback<EmailCriticoTemplate, string, string>((_, _, s) => segredo = s);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "novo@test.com"));

        captured.Should().NotBeNull();
        captured!.ContaId.Should().Be(conta.Id);
        captured.NovoEmail.Should().Be("novo@test.com");
        captured.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$");
        captured.ExpiraEm.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(30));
        captured.UsadoEm.Should().BeNull();

        segredo.Should().NotBeNullOrEmpty();
        Hash(segredo!).Should().Be(captured.TokenHash);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-email")]
    public async Task HandleAsync_NovoEmailInvalido_LancaValidationException(string novoEmail)
    {
        var act = async () => await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, novoEmail));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_NovoEmailEmUso_LogNaoContemEmailCru()
    {
        var contaAtual = BuildConta("atual@test.com");
        var outraConta = BuildConta("novo@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(contaAtual);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(outraConta);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "novo@test.com"));

        _logger.Verify(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("novo@test.com")),
            It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
