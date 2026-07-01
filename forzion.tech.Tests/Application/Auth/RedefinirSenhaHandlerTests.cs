using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.RedefinirSenha;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Auth;

public class RedefinirSenhaHandlerTests
{
    private readonly Mock<IPasswordResetTokenRepository> _tokenRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IRedefinicaoSenhaSegundoFatorRepository> _segundoFator = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedDevice = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<IMfaSecretProtector> _protector = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<ILogger<RedefinirSenhaHandler>> _logger = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly RedefinirSenhaCommandValidator _validator = new(new FakePwnedPasswordsService());
    private readonly RedefinirSenhaHandler _handler;

    private const string RawToken = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";

    public RedefinirSenhaHandlerTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(s => $"hash:{s}");
        _handler = new RedefinirSenhaHandler(
            _tokenRepo.Object, _contaRepo.Object, _mfaRepo.Object, _segundoFator.Object, _trustedDevice.Object,
            _hasher.Object, _refresh.Object, _totp.Object, _protector.Object,
            _logRepo.Object, _unitOfWork.Object, _timeProvider, _validator, _logger.Object);
    }

    private ContaMfa MfaHabilitado(Guid contaId)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var mfa = ContaMfa.Criar(contaId, "cifrado", agora).Value;
        mfa.Confirmar(50, agora);
        return mfa;
    }

    private static string ComputeHash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private PasswordResetToken BuildToken(Guid? contaId = null, TimeSpan? ttl = null, DateTime? usedAt = null)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var token = PasswordResetToken.Criar(
            contaId ?? Guid.NewGuid(),
            ComputeHash(RawToken),
            agora.Add(ttl ?? TimeSpan.FromMinutes(30)),
            agora).Value;
        if (usedAt.HasValue)
            token.MarcarComoUsado(usedAt.Value);
        return token;
    }

    private static Conta BuildConta() =>
        Conta.Criar(Email.Criar("user@example.com").Value, "old-hash", TipoConta.Aluno, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_TokenInexistente_LancaDomainException()
    {
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetToken?)null);

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("inválido");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TokenJaUsado_LancaDomainException()
    {
        var token = BuildToken(usedAt: _timeProvider.GetUtcNow().UtcDateTime);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("inválido ou já utilizado");
    }

    [Fact]
    public async Task HandleAsync_TokenExpirado_LancaDomainException()
    {
        var token = BuildToken(ttl: TimeSpan.FromMinutes(1));
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2)); // token agora expirado

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("expirado");
    }

    [Fact]
    public async Task HandleAsync_TokenValido_AtualizaSenhaEMarcaUsado()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsSuccess.Should().BeTrue();
        conta.PasswordHash.Should().Be("hash:NovaSenha123");
        token.UsedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TokenValido_CarimbaEpochDeSessao()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsSuccess.Should().BeTrue();
        conta.SessoesInvalidasAntesDeUtc.Should().Be(_timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task HandleAsync_TokenValido_RevogaDispositivosConfiaveis()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsSuccess.Should().BeTrue();
        _trustedDevice.Verify(r => r.RemoverPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MfaHabilitado_SemCodigoTotp_NaoEfetivaENaoConsumeToken()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado(conta.Id));

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.codigo_invalido");
        token.UsedAt.Should().BeNull();
        _segundoFator.Verify(r => r.AdicionarAsync(It.IsAny<RedefinicaoSenhaSegundoFator>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MfaHabilitado_CodigoTotpInvalido_NaoEfetiva()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado(conta.Id));
        _protector.Setup(p => p.Revelar("cifrado")).Returns("SECRET");
        _totp.Setup(t => t.Verificar("SECRET", "000000", 50)).Returns(new TotpVerificacao(false, 0));

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123", "000000"));

        result.IsFailure.Should().BeTrue();
        token.UsedAt.Should().BeNull();
        conta.PasswordHash.Should().Be("old-hash");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MfaHabilitado_CodigoTotpValido_AtualizaSenhaERegistraTimeStep()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        var mfa = MfaHabilitado(conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _protector.Setup(p => p.Revelar("cifrado")).Returns("SECRET");
        _totp.Setup(t => t.Verificar("SECRET", "123456", 50)).Returns(new TotpVerificacao(true, 100));

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123", "123456"));

        result.IsSuccess.Should().BeTrue();
        conta.PasswordHash.Should().Be("hash:NovaSenha123");
        token.UsedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        mfa.UltimoTimeStep.Should().Be(100);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Replay_MesmoTokenDuasVezes_SegundaFalha()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var primeira = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));
        primeira.IsSuccess.Should().BeTrue();

        var segunda = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "OutraSenha123"));

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Message.Should().Contain("inválido ou já utilizado");
    }

    [Fact]
    public async Task HandleAsync_ContaAnonimizada_RecusaSemReporHash()
    {
        var conta = BuildConta();
        conta.Anonimizar(_timeProvider.GetUtcNow().UtcDateTime);
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth_reset.token_invalido");
        conta.PasswordHash.Should().BeEmpty();
        token.UsedAt.Should().BeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_LancaEstadoInconsistente()
    {
        var token = BuildToken();
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));
        await act.Should().ThrowAsync<EstadoInconsistenteException>().WithMessage("*Conta não encontrada*");
    }

    [Theory]
    [InlineData("", "*obrigatório*")]
    [InlineData("short", "*inválido*")]
    public async Task HandleAsync_TokenInvalido_FalhaValidacao(string token, string messageContains)
    {
        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(token, "NovaSenha123"));
        await act.Should().ThrowAsync<ValidationException>().WithMessage(messageContains);
    }

    [Theory]
    [InlineData("curta1A", "*12 caracteres*")]
    [InlineData("semuppercase1", "*maiúscula*")]
    [InlineData("SEMLOWERCASE1", "*minúscula*")]
    [InlineData("SemDigitoAbcd", "*dígito*")]
    public async Task HandleAsync_SenhaFraca_FalhaValidacao(string senha, string messageContains)
    {
        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, senha));
        await act.Should().ThrowAsync<ValidationException>().WithMessage(messageContains);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_TokenValido_RegistraLogSenhaRedefinida()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123"));

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.SenhaRedefinida), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SegundoFatorInvalido_FalhasRepetidas_BloqueiaPorContaImuneARotacaoDeIp()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        var mfa = MfaHabilitado(conta.Id);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var guard = RedefinicaoSenhaSegundoFator.Criar(conta.Id, agora).Value;

        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _segundoFator.Setup(r => r.BuscarPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(guard);
        _protector.Setup(p => p.Revelar("cifrado")).Returns("SECRET");
        _totp.Setup(t => t.Verificar("SECRET", "000000", It.IsAny<long?>())).Returns(new TotpVerificacao(false, 0));

        Result ultimaFalha = Result.Success();
        for (var i = 0; i < RedefinicaoSenhaSegundoFator.MaximoTentativas; i++)
            ultimaFalha = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123", "000000"));

        ultimaFalha.Error!.Code.Should().Be("mfa.codigo_invalido");

        var bloqueado = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123", "000000"));

        bloqueado.IsFailure.Should().BeTrue();
        bloqueado.Error!.Code.Should().Be("auth_reset.segundo_fator_bloqueado");
        token.UsedAt.Should().BeNull();
        _totp.Verify(t => t.Verificar("SECRET", "000000", It.IsAny<long?>()), Times.Exactly(RedefinicaoSenhaSegundoFator.MaximoTentativas));
    }

    [Fact]
    public async Task HandleAsync_SegundoFatorValido_DentroDoCap_Prossegue()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        var mfa = MfaHabilitado(conta.Id);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var guard = RedefinicaoSenhaSegundoFator.Criar(conta.Id, agora).Value;
        guard.RegistrarFalha(agora);

        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _segundoFator.Setup(r => r.BuscarPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(guard);
        _protector.Setup(p => p.Revelar("cifrado")).Returns("SECRET");
        _totp.Setup(t => t.Verificar("SECRET", "123456", 50)).Returns(new TotpVerificacao(true, 100));

        var result = await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha123", "123456"));

        result.IsSuccess.Should().BeTrue();
        conta.PasswordHash.Should().Be("hash:NovaSenha123");
        token.UsedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
