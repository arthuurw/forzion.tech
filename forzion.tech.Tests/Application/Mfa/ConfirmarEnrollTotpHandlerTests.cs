using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Mfa;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OtpNet;

namespace forzion.tech.Tests.Application.Mfa;

public class ConfirmarEnrollTotpHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaMfaRepository> _contaMfaRepository = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OtpNetTotpService _totp = new();
    private readonly MfaSecretProtector _protector = new(new byte[32]);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Agora));
    private readonly ConfirmarEnrollTotpCommandValidator _validator = new();

    public ConfirmarEnrollTotpHandlerTests() =>
        _userContext.Setup(u => u.ContaId).Returns(ContaId);

    private ConfirmarEnrollTotpHandler CriarHandler() => new(
        _userContext.Object, _contaMfaRepository.Object, _recoveryRepository.Object,
        _totp, _protector, _unitOfWork.Object, _time, _validator);

    private (ContaMfa Mfa, string Secret) PendenteComSecret()
    {
        var secret = _totp.GerarSecret();
        var mfa = ContaMfa.Criar(ContaId, _protector.Proteger(secret), Agora).Value;
        _contaMfaRepository.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);
        return (mfa, secret);
    }

    private static string CodigoAtual(string secret) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    [Fact]
    public async Task Confirmar_CodigoValido_HabilitaEGera10Recovery()
    {
        var (mfa, secret) = PendenteComSecret();
        IEnumerable<MfaRecoveryCode>? salvos = null;
        _recoveryRepository.Setup(r => r.AdicionarRangeAsync(It.IsAny<IEnumerable<MfaRecoveryCode>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<MfaRecoveryCode>, CancellationToken>((c, _) => salvos = c.ToList());

        var result = await CriarHandler().HandleAsync(new ConfirmarEnrollTotpCommand(CodigoAtual(secret)));

        result.IsSuccess.Should().BeTrue();
        result.Value.RecoveryCodes.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        mfa.Habilitado.Should().BeTrue();
        salvos.Should().NotBeNull();
        salvos!.Should().HaveCount(10);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmar_CodigoInvalido_FalhaNaoHabilita()
    {
        var (mfa, _) = PendenteComSecret();

        var result = await CriarHandler().HandleAsync(new ConfirmarEnrollTotpCommand("000000"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(MfaErrors.CodigoInvalido.Code);
        mfa.Habilitado.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_EnrollNaoIniciado_Falha()
    {
        _contaMfaRepository.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);

        var result = await CriarHandler().HandleAsync(new ConfirmarEnrollTotpCommand("123456"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(MfaErrors.EnrollNaoIniciado.Code);
    }

    [Fact]
    public async Task Confirmar_JaHabilitado_Falha()
    {
        var (mfa, secret) = PendenteComSecret();
        mfa.Confirmar(1, Agora);

        var result = await CriarHandler().HandleAsync(new ConfirmarEnrollTotpCommand(CodigoAtual(secret)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(MfaErrors.JaConfirmado.Code);
    }
}
