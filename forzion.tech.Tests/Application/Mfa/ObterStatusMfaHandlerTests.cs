using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Mfa;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Mfa;

public class ObterStatusMfaHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedRepo = new();
    private readonly ObterStatusMfaHandler _handler;

    public ObterStatusMfaHandlerTests()
    {
        _userContext.SetupGet(u => u.ContaId).Returns(ContaId);
        _handler = new ObterStatusMfaHandler(
            _userContext.Object, _mfaRepo.Object, _recoveryRepo.Object, _trustedRepo.Object,
            new FakeTimeProvider(new DateTimeOffset(Agora)));
    }

    private static ContaMfa MfaHabilitado()
    {
        var mfa = ContaMfa.Criar(ContaId, "cifrado", Agora).Value;
        mfa.Confirmar(50, Agora);
        return mfa;
    }

    [Fact]
    public async Task Status_Habilitado_ReflexteRecoveryRestantesEDispositivosAtivos()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado());

        var usado = MfaRecoveryCode.Criar(ContaId, "h1", Agora).Value;
        usado.MarcarUsado(Agora);
        var disponivel1 = MfaRecoveryCode.Criar(ContaId, "h2", Agora).Value;
        var disponivel2 = MfaRecoveryCode.Criar(ContaId, "h3", Agora).Value;
        _recoveryRepo.Setup(r => r.ListarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { usado, disponivel1, disponivel2 });

        var ativo = TrustedDevice.Criar(ContaId, "t1", Agora.AddDays(30), Agora, "iPhone").Value;
        var expirado = TrustedDevice.Criar(ContaId, "t2", Agora.AddDays(-10), Agora.AddDays(-40)).Value;
        _trustedRepo.Setup(r => r.ListarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ativo, expirado });

        var status = await _handler.HandleAsync();

        status.Habilitado.Should().BeTrue();
        status.RecoveryCodesRestantes.Should().Be(2);
        status.Dispositivos.Should().ContainSingle().Which.Id.Should().Be(ativo.Id);
    }

    [Fact]
    public async Task Status_Desabilitado_RetornaVazio()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);

        var status = await _handler.HandleAsync();

        status.Habilitado.Should().BeFalse();
        status.RecoveryCodesRestantes.Should().Be(0);
        status.Dispositivos.Should().BeEmpty();
        _recoveryRepo.Verify(r => r.ListarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
