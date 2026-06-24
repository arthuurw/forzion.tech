using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Auth.Mfa;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Mfa;

public class CompletarLoginMfaHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Jti = Guid.NewGuid();
    private static readonly Guid PerfilId = Guid.NewGuid();

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<IMfaChallengeRepository> _challengeRepo = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedRepo = new();
    private readonly Mock<ITokenRevogadoRepository> _tokenRevogadoRepo = new();
    private readonly Mock<ILoginPerfilResolver> _perfilResolver = new();
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<IMfaSecretProtector> _protector = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CompletarLoginMfaHandler>> _logger = new();
    private readonly CompletarLoginMfaCommandValidator _validator = new();
    private readonly CompletarLoginMfaHandler _handler;
    private readonly Conta _conta;

    public CompletarLoginMfaHandlerTests()
    {
        _conta = Conta.Criar(Email.Criar("u@test.com").Value, "hash", TipoConta.Treinador, Agora).Value;
        _userContext.SetupGet(u => u.ContaId).Returns(_conta.Id);
        _userContext.SetupGet(u => u.Jti).Returns(Jti);
        _userContext.SetupGet(u => u.TokenExpiraEm).Returns(Agora.AddMinutes(5));
        _contaRepo.Setup(r => r.ObterPorIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_conta);
        _perfilResolver.Setup(r => r.ResolverAsync(It.IsAny<Conta>(), It.IsAny<CancellationToken>())).ReturnsAsync((PerfilId, "João Trainer"));
        _refresh.Setup(s => s.EmitirNovaFamiliaAsync(It.IsAny<Conta>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshEmitido("refresh-raw", Guid.NewGuid()));
        _jwt.Setup(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>())).Returns("token.jwt");
        _protector.Setup(p => p.Revelar(It.IsAny<string>())).Returns("SECRET");

        _handler = new CompletarLoginMfaHandler(
            _userContext.Object, _contaRepo.Object, _mfaRepo.Object, _recoveryRepo.Object, _challengeRepo.Object,
            _trustedRepo.Object, _tokenRevogadoRepo.Object, _perfilResolver.Object, _totp.Object, _protector.Object,
            _jwt.Object, _refresh.Object, _unitOfWork.Object, new FakeTimeProvider(new DateTimeOffset(Agora)), _validator,
            _logger.Object);
    }

    private ContaMfa MfaHabilitado()
    {
        var mfa = ContaMfa.Criar(_conta.Id, "cifrado", Agora).Value;
        mfa.Confirmar(50, Agora);
        return mfa;
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    [Fact]
    public async Task Verificar_TotpValido_EmiteLoginRevogaPendenteECommita()
    {
        var mfa = MfaHabilitado();
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _totp.Setup(t => t.Verificar("SECRET", "123456", 50)).Returns(new TotpVerificacao(true, 100));

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("123456", MfaFator.Totp));

        result.IsSuccess.Should().BeTrue();
        result.Value.Login.Token.Should().Be("token.jwt");
        result.Value.Login.MfaRequerido.Should().BeFalse();
        result.Value.TrustedDeviceToken.Should().BeNull();
        mfa.UltimoTimeStep.Should().Be(100);
        _tokenRevogadoRepo.Verify(r => r.AdicionarAsync(It.Is<TokenRevogado>(t => t.Jti == Jti), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_TotpInvalido_FalhaCommitaSemTokenELogaWarning()
    {
        var mfa = MfaHabilitado();
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _totp.Setup(t => t.Verificar("SECRET", "000000", 50)).Returns(new TotpVerificacao(false, 0));

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("000000", MfaFator.Totp));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.codigo_invalido");
        _jwt.Verify(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("MFA")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_RecoveryValido_ConsomeCodigoEEmiteLogin()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado());
        var correto = MfaRecoveryCode.Criar(_conta.Id, Hash("abcde12345"), Agora).Value;
        var outro = MfaRecoveryCode.Criar(_conta.Id, Hash("99999zzzzz"), Agora).Value;
        _recoveryRepo.Setup(r => r.ListarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { outro, correto });

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("abcde12345", MfaFator.RecoveryCode));

        result.IsSuccess.Should().BeTrue();
        correto.Disponivel.Should().BeFalse();
        outro.Disponivel.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_RecoveryInexistente_Falha()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado());
        _recoveryRepo.Setup(r => r.ListarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MfaRecoveryCode.Criar(_conta.Id, Hash("outro00000"), Agora).Value });

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("naoexiste0", MfaFator.RecoveryCode));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.codigo_invalido");
        _jwt.Verify(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Verificar_EmailValido_MarcaChallengeUsadoEEmiteLogin()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado());
        var challenge = MfaChallenge.Criar(_conta.Id, Hash("654321"), MfaProposito.LoginFallback, Agora.AddMinutes(10), Agora).Value;
        _challengeRepo.Setup(r => r.BuscarUltimoPorContaEPropositoAsync(_conta.Id, MfaProposito.LoginFallback, It.IsAny<CancellationToken>())).ReturnsAsync(challenge);

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("654321", MfaFator.Email));

        result.IsSuccess.Should().BeTrue();
        result.Value.Login.Token.Should().Be("token.jwt");
        challenge.UsadoEm.Should().NotBeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_NaoHabilitado_Falha()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("123456", MfaFator.Totp));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.nao_habilitado");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verificar_LembrarDispositivo_CriaTrustedDeviceERetornaToken()
    {
        var mfa = MfaHabilitado();
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _totp.Setup(t => t.Verificar("SECRET", "123456", 50)).Returns(new TotpVerificacao(true, 101));

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("123456", MfaFator.Totp, LembrarDispositivo: true, Rotulo: "Firefox"));

        result.IsSuccess.Should().BeTrue();
        result.Value.TrustedDeviceToken.Should().NotBeNullOrEmpty();
        _trustedRepo.Verify(r => r.AdicionarAsync(It.Is<TrustedDevice>(d => d.ContaId == _conta.Id && d.Rotulo == "Firefox"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_ContaInexistente_Falha()
    {
        _contaRepo.Setup(r => r.ObterPorIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new CompletarLoginMfaCommand("123456", MfaFator.Totp));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.conta_id_invalido");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
