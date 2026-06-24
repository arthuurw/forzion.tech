using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.StepUp;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Application.Auth.StepUp;

public class VerificarStepUpHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IMfaChallengeRepository> _challengeRepo = new();
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<IMfaSecretProtector> _protector = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Agora));
    private readonly Mock<ILogger<VerificarStepUpHandler>> _logger = new();
    private readonly VerificarStepUpHandler _handler;
    private readonly Conta _conta;

    public VerificarStepUpHandlerTests()
    {
        _conta = Conta.Criar(DomainEmail.Criar("user@example.com").Value, "hash", TipoConta.Aluno, Agora).Value;
        _userContext.SetupGet(u => u.ContaId).Returns(_conta.Id);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(_conta);
        _jwt.Setup(j => j.GerarTokenEscopo(It.IsAny<Conta>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(new TokenEscopo("step-up-token", Guid.NewGuid(), Agora.AddMinutes(5)));

        _handler = new VerificarStepUpHandler(
            _userContext.Object, _contaRepo.Object, _mfaRepo.Object, _challengeRepo.Object,
            _totp.Object, _protector.Object, _jwt.Object, _unitOfWork.Object, _time,
            new VerificarStepUpCommandValidator(), _logger.Object);
    }

    private static string Hash(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();

    private ContaMfa MfaHabilitado()
    {
        var mfa = ContaMfa.Criar(_conta.Id, "cifrado", Agora).Value;
        mfa.Confirmar(50, Agora);
        return mfa;
    }

    [Fact]
    public async Task Verificar_TotpValido_EmiteTokenERegistraUso()
    {
        var mfa = MfaHabilitado();
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _protector.Setup(p => p.Revelar(It.IsAny<string>())).Returns("SECRET");
        _totp.Setup(t => t.Verificar("SECRET", "123456", It.IsAny<long?>())).Returns(new TotpVerificacao(true, 100));

        var result = await _handler.HandleAsync(new VerificarStepUpCommand("123456"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("step-up-token");
        mfa.UltimoTimeStep.Should().Be(100);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_TotpInvalido_FalhaSemEmitirToken()
    {
        var mfa = MfaHabilitado();
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        _protector.Setup(p => p.Revelar(It.IsAny<string>())).Returns("SECRET");
        _totp.Setup(t => t.Verificar(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>())).Returns(new TotpVerificacao(false, 0));

        var result = await _handler.HandleAsync(new VerificarStepUpCommand("000000"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.codigo_invalido");
        _jwt.Verify(j => j.GerarTokenEscopo(It.IsAny<Conta>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
        _logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("step-up")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_EmailCodigoCorreto_EmiteTokenEMarcaUsado()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);
        var challenge = MfaChallenge.Criar(_conta.Id, Hash("654321"), MfaProposito.StepUp, Agora.AddMinutes(10), Agora).Value;
        _challengeRepo.Setup(r => r.BuscarUltimoPorContaEPropositoAsync(It.IsAny<Guid>(), MfaProposito.StepUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(challenge);

        var result = await _handler.HandleAsync(new VerificarStepUpCommand("654321"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("step-up-token");
        challenge.UsadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Verificar_EmailCodigoErrado_ContaTentativaEFalha()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);
        var challenge = MfaChallenge.Criar(_conta.Id, Hash("111111"), MfaProposito.StepUp, Agora.AddMinutes(10), Agora).Value;
        _challengeRepo.Setup(r => r.BuscarUltimoPorContaEPropositoAsync(It.IsAny<Guid>(), MfaProposito.StepUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(challenge);

        var result = await _handler.HandleAsync(new VerificarStepUpCommand("999999"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.codigo_invalido");
        challenge.Tentativas.Should().Be(1);
        challenge.UsadoEm.Should().BeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verificar_EmailChallengeExpirado_Falha()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);
        var challenge = MfaChallenge.Criar(_conta.Id, Hash("654321"), MfaProposito.StepUp, Agora.AddMinutes(-1), Agora.AddMinutes(-10)).Value;
        _challengeRepo.Setup(r => r.BuscarUltimoPorContaEPropositoAsync(It.IsAny<Guid>(), MfaProposito.StepUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(challenge);

        var result = await _handler.HandleAsync(new VerificarStepUpCommand("654321"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.challenge_expirado");
    }

    [Fact]
    public async Task Verificar_EmailChallengeBloqueadoPorTentativas_Falha()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);
        var challenge = MfaChallenge.Criar(_conta.Id, Hash("654321"), MfaProposito.StepUp, Agora.AddMinutes(10), Agora).Value;
        for (var i = 0; i < MfaChallenge.MaximoTentativas; i++)
            challenge.RegistrarTentativa();
        _challengeRepo.Setup(r => r.BuscarUltimoPorContaEPropositoAsync(It.IsAny<Guid>(), MfaProposito.StepUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(challenge);

        var result = await _handler.HandleAsync(new VerificarStepUpCommand("654321"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.challenge_bloqueado");
    }
}
