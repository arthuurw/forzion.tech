using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Mfa;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Mfa;

public class RegenerarRecoveryCodesHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RegenerarRecoveryCodesHandler _handler;

    public RegenerarRecoveryCodesHandlerTests()
    {
        _userContext.SetupGet(u => u.ContaId).Returns(ContaId);
        _handler = new RegenerarRecoveryCodesHandler(
            _userContext.Object, _mfaRepo.Object, _recoveryRepo.Object, _unitOfWork.Object,
            new FakeTimeProvider(new DateTimeOffset(Agora)));
    }

    private static ContaMfa MfaHabilitado()
    {
        var mfa = ContaMfa.Criar(ContaId, "cifrado", Agora).Value;
        mfa.Confirmar(50, Agora);
        return mfa;
    }

    [Fact]
    public async Task Regenerar_Habilitado_InvalidaLoteAnteriorEGera10()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado());
        List<MfaRecoveryCode>? adicionados = null;
        _recoveryRepo.Setup(r => r.AdicionarRangeAsync(It.IsAny<IEnumerable<MfaRecoveryCode>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<MfaRecoveryCode>, CancellationToken>((c, _) => adicionados = c.ToList())
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.RecoveryCodes.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        adicionados.Should().HaveCount(10);
        _recoveryRepo.Verify(r => r.RemoverPorContaIdAsync(ContaId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Regenerar_NaoHabilitado_FalhaSemCommit()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);

        var result = await _handler.HandleAsync();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa.nao_habilitado");
        _recoveryRepo.Verify(r => r.RemoverPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
