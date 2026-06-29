using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using DomainConta = forzion.tech.Domain.Entities.Conta;

namespace forzion.tech.Tests.Application.ContaTestes;

public class AlterarSenhaHandlerTests
{
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedDevice = new();
    private readonly Mock<ITokenRevogadoRepository> _tokenRevogado = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AlterarSenhaCommand>> _validator = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<ILogger<AlterarSenhaHandler>> _logger = new();
    private readonly AlterarSenhaHandler _handler;

    public AlterarSenhaHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<AlterarSenhaCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _handler = new AlterarSenhaHandler(
            _userContext.Object,
            _contaRepo.Object,
            _passwordHasher.Object,
            _refresh.Object,
            _trustedDevice.Object,
            _tokenRevogado.Object,
            _logRepo.Object,
            _unitOfWork.Object,
            TimeProvider.System,
            _validator.Object,
            _logger.Object);
    }

    private static DomainConta CriarConta() =>
        DomainConta.Criar(Email.Criar("user@test.com").Value, "hash-atual", TipoConta.Aluno, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_SenhaCorreta_AtualizaEComita()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify("senha123", conta.PasswordHash)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("nova-senha")).Returns("novo-hash");

        var result = await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _refresh.Verify(s => s.RevogarTodasPorContaAsync(conta.Id, MotivoRevogacaoFamilia.TrocaSenha, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SenhaCorreta_RevogaDispositivosConfiaveis()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify("senha123", conta.PasswordHash)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("nova-senha")).Returns("novo-hash");

        var result = await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));

        result.IsSuccess.Should().BeTrue();
        _trustedDevice.Verify(r => r.RemoverPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SenhaCorreta_CarimbaEpochDeSessao()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify("senha123", conta.PasswordHash)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("nova-senha")).Returns("novo-hash");

        var result = await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));

        result.IsSuccess.Should().BeTrue();
        conta.SessoesInvalidasAntesDeUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_JtiCorrenteValido_FazBlacklistDoToken()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();
        var jti = Guid.NewGuid();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddMinutes(15));
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify("senha123", conta.PasswordHash)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("nova-senha")).Returns("novo-hash");

        var result = await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));

        result.IsSuccess.Should().BeTrue();
        _tokenRevogado.Verify(r => r.AdicionarAsync(It.Is<TokenRevogado>(t => t.Jti == jti), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemJti_NaoTentaBlacklist()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.Jti).Returns(Guid.Empty);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify("senha123", conta.PasswordHash)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("nova-senha")).Returns("novo-hash");

        var result = await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));

        result.IsSuccess.Should().BeTrue();
        _tokenRevogado.Verify(r => r.AdicionarAsync(It.IsAny<TokenRevogado>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SenhaIncorreta_LancaCredenciaisInvalidasException()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), conta.PasswordHash)).Returns(false);

        var act = async () => await _handler.HandleAsync(new AlterarSenhaCommand("errada", "nova-senha"));
        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_LancaEstadoInconsistente()
    {
        var contaId = Guid.NewGuid();
        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainConta?)null);

        var act = async () => await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));
        await act.Should().ThrowAsync<EstadoInconsistenteException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static readonly AlterarSenhaCommandValidator _realValidator = new();

    [Theory]
    [InlineData("", "Senha@123")]
    [InlineData("atual", "")]
    [InlineData("atual", "Ab1")]
    [InlineData("atual", "abcdefg1")]
    [InlineData("atual", "ABCDEFG1")]
    [InlineData("atual", "Abcdefgh")]
    public void Validator_CommandInvalido_Falha(string senhaAtual, string novaSenha)
    {
        var result = _realValidator.Validate(new AlterarSenhaCommand(senhaAtual, novaSenha));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_CommandValido_Passa()
    {
        var result = _realValidator.Validate(new AlterarSenhaCommand("SenhaAtual1", "NovaSenha@9"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_SenhaCorreta_RegistraLogSenhaAlterada()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify("senha123", conta.PasswordHash)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("nova-senha")).Returns("novo-hash");

        var result = await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.SenhaAlterada), It.IsAny<CancellationToken>()), Times.Once);
    }
}
