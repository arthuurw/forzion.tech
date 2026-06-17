using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.StepUp;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Application.Auth.StepUp;

public class IniciarStepUpHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IEnviarCodigoMfaService> _enviarCodigo = new();
    private readonly IniciarStepUpHandler _handler;

    public IniciarStepUpHandlerTests()
    {
        _handler = new IniciarStepUpHandler(_userContext.Object, _contaRepo.Object, _mfaRepo.Object, _enviarCodigo.Object);
    }

    private static Conta BuildConta() =>
        Conta.Criar(DomainEmail.Criar("user@example.com").Value, "hash", TipoConta.Aluno, Agora).Value;

    private static ContaMfa BuildMfaHabilitado(Guid contaId)
    {
        var mfa = ContaMfa.Criar(contaId, "cifrado", Agora).Value;
        mfa.Confirmar(50, Agora);
        return mfa;
    }

    [Fact]
    public async Task Iniciar_MfaHabilitado_RetornaTotpSemEnviarEmail()
    {
        var conta = BuildConta();
        _userContext.SetupGet(u => u.ContaId).Returns(conta.Id);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildMfaHabilitado(conta.Id));

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Fator.Should().Be(MfaFator.Totp);
        _enviarCodigo.Verify(s => s.EnviarAsync(It.IsAny<Conta>(), It.IsAny<MfaProposito>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Iniciar_MfaDesabilitado_EnviaCodigoEmail()
    {
        var conta = BuildConta();
        _userContext.SetupGet(u => u.ContaId).Returns(conta.Id);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Fator.Should().Be(MfaFator.Email);
        _enviarCodigo.Verify(s => s.EnviarAsync(conta, MfaProposito.StepUp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Iniciar_ContaInexistente_Falha()
    {
        _userContext.SetupGet(u => u.ContaId).Returns(Guid.NewGuid());
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync();

        result.IsFailure.Should().BeTrue();
        _enviarCodigo.Verify(s => s.EnviarAsync(It.IsAny<Conta>(), It.IsAny<MfaProposito>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
