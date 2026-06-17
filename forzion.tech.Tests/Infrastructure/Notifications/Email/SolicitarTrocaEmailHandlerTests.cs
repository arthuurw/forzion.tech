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
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IEmailBackgroundDispatcher> _dispatcher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<SolicitarTrocaEmailHandler>> _logger = new();
    private readonly SolicitarTrocaEmailHandler _handler;

    private static readonly Guid ContaId = Guid.NewGuid();

    public SolicitarTrocaEmailHandlerTests()
    {
        _emailService.SetupGet(s => s.Habilitado).Returns(true);
        _dispatcher.Setup(d => d.Disparar(It.IsAny<Func<IEmailService, CancellationToken, Task>>()))
            .Callback<Func<IEmailService, CancellationToken, Task>>(f => f(_emailService.Object, CancellationToken.None).GetAwaiter().GetResult());

        _handler = new SolicitarTrocaEmailHandler(
            _contaRepo.Object,
            _tokenRepo.Object,
            _dispatcher.Object,
            _unitOfWork.Object,
            _timeProvider,
            _logger.Object,
            new SolicitarTrocaEmailCommandValidator());
    }

    private Conta BuildConta(string email = "atual@test.com") =>
        Conta.Criar(DomainEmail.Criar(email).Value, "hash", TipoConta.Aluno, _timeProvider.GetUtcNow().UtcDateTime).Value;

    [Fact]
    public async Task HandleAsync_NovoEmailIgualAoAtual_NaoCriaTokenNemEnviaEmail()
    {
        var conta = BuildConta("atual@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "ATUAL@test.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<TrocaEmailToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NovoEmailEmUsoPorOutraConta_RespostaGenericaSemTokenSemEmail()
    {
        var contaAtual = BuildConta("atual@test.com");
        var outraConta = BuildConta("novo@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(contaAtual);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(outraConta);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "novo@test.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<TrocaEmailToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NovoEmailDisponivel_CriaTokenHashedComExpiracao30min_EEnviaAoNovoEmail()
    {
        var conta = BuildConta("atual@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        TrocaEmailToken? captured = null;
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<TrocaEmailToken>(), It.IsAny<CancellationToken>()))
            .Callback<TrocaEmailToken, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "novo@test.com"));

        captured.Should().NotBeNull();
        captured!.ContaId.Should().Be(conta.Id);
        captured.NovoEmail.Should().Be("novo@test.com");
        captured.TokenHash.Should().HaveLength(64);
        captured.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$");
        captured.ExpiraEm.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(30));
        captured.UsadoEm.Should().BeNull();

        _emailService.Verify(s => s.EnviarAsync(
            "novo@test.com",
            It.Is<string>(a => a.Contains("troca de e-mail", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_CriaTokenMasNaoEnvia()
    {
        _emailService.SetupGet(s => s.Habilitado).Returns(false);
        var conta = BuildConta("atual@test.com");
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(new SolicitarTrocaEmailCommand(ContaId, "novo@test.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<TrocaEmailToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
}
