using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.Mfa;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Moq;

namespace forzion.tech.Tests.Application.Mfa;

public class SolicitarCodigoLoginEmailHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IContaMfaRepository> _mfaRepo = new();
    private readonly Mock<IEnviarCodigoMfaService> _enviar = new();
    private readonly SolicitarCodigoLoginEmailHandler _handler;
    private readonly Conta _conta;

    public SolicitarCodigoLoginEmailHandlerTests()
    {
        _conta = Conta.Criar(Email.Criar("u@test.com").Value, "hash", TipoConta.Treinador, Agora).Value;
        _userContext.SetupGet(u => u.ContaId).Returns(_conta.Id);
        _contaRepo.Setup(r => r.ObterPorIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_conta);
        _handler = new SolicitarCodigoLoginEmailHandler(_userContext.Object, _contaRepo.Object, _mfaRepo.Object, _enviar.Object);
    }

    private ContaMfa MfaHabilitado()
    {
        var mfa = ContaMfa.Criar(_conta.Id, "cifrado", Agora).Value;
        mfa.Confirmar(1, Agora);
        return mfa;
    }

    [Fact]
    public async Task Solicitar_MfaHabilitado_EnviaCodigoLoginFallback()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(MfaHabilitado());

        await _handler.HandleAsync();

        _enviar.Verify(s => s.EnviarAsync(_conta, MfaProposito.LoginFallback, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Solicitar_MfaNaoHabilitado_NaoEnvia()
    {
        _mfaRepo.Setup(r => r.BuscarPorContaIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);

        await _handler.HandleAsync();

        _enviar.Verify(s => s.EnviarAsync(It.IsAny<Conta>(), It.IsAny<MfaProposito>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Solicitar_ContaInexistente_NaoEnvia()
    {
        _contaRepo.Setup(r => r.ObterPorIdAsync(_conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        await _handler.HandleAsync();

        _enviar.Verify(s => s.EnviarAsync(It.IsAny<Conta>(), It.IsAny<MfaProposito>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
