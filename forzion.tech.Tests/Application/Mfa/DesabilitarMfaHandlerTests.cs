using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Mfa;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Mfa;

public class DesabilitarMfaHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid Jti = Guid.NewGuid();

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedRepo = new();
    private readonly Mock<ITokenRevogadoRepository> _tokenRevogadoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly DesabilitarMfaHandler _handler;

    public DesabilitarMfaHandlerTests()
    {
        _userContext.SetupGet(u => u.ContaId).Returns(ContaId);
        _userContext.SetupGet(u => u.Jti).Returns(Jti);
        _userContext.SetupGet(u => u.TokenExpiraEm).Returns(Agora.AddHours(1));
        _handler = new DesabilitarMfaHandler(
            _userContext.Object, _mfaRepo.Object, _recoveryRepo.Object, _trustedRepo.Object,
            _tokenRevogadoRepo.Object, _unitOfWork.Object, new FakeTimeProvider(new DateTimeOffset(Agora)));
    }

    private static ContaMfa MfaHabilitado()
    {
        var mfa = ContaMfa.Criar(ContaId, "cifrado", Agora).Value;
        mfa.Confirmar(50, Agora);
        return mfa;
    }

    [Fact]
    public async Task Desabilitar_Habilitado_LimpaSecretRecoveryDevicesERevogaJti()
    {
        var mfa = MfaHabilitado();
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(mfa);

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        mfa.Habilitado.Should().BeFalse();
        mfa.TotpSecretCifrado.Should().BeNull();
        _recoveryRepo.Verify(r => r.RemoverPorContaIdAsync(ContaId, It.IsAny<CancellationToken>()), Times.Once);
        _trustedRepo.Verify(r => r.RemoverPorContaIdAsync(ContaId, It.IsAny<CancellationToken>()), Times.Once);
        _tokenRevogadoRepo.Verify(r => r.AdicionarAsync(It.Is<TokenRevogado>(t => t.Jti == Jti), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desabilitar_NaoHabilitado_FalhaSemCommit()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);

        var result = await _handler.HandleAsync();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.nao_habilitado");
        _recoveryRepo.Verify(r => r.RemoverPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
