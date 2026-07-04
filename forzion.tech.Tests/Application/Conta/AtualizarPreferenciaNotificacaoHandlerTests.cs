using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.PreferenciasNotificacao;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.ContaTestes;

public class AtualizarPreferenciaNotificacaoHandlerTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly AtualizarPreferenciaNotificacaoHandler _handler;

    public AtualizarPreferenciaNotificacaoHandlerTests()
    {
        _userContext.SetupGet(u => u.ContaId).Returns(ContaId);
        _handler = new AtualizarPreferenciaNotificacaoHandler(
            _userContext.Object, _contaRepo.Object, _unitOfWork.Object, _time);
    }

    [Fact]
    public async Task HandleAsync_OptOutTrue_PersisteFlagECommita()
    {
        var conta = new ContaBuilder().Build();
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new AtualizarPreferenciaNotificacaoCommand(true));

        result.IsSuccess.Should().BeTrue();
        conta.NotificacoesEngajamentoEmailOptOut.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OptOutFalse_ReativaEmail()
    {
        var conta = new ContaBuilder().ComEngajamentoEmailOptOut().Build();
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new AtualizarPreferenciaNotificacaoCommand(false));

        result.IsSuccess.Should().BeTrue();
        conta.NotificacoesEngajamentoEmailOptOut.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UsaContaDoToken_NuncaDoCliente()
    {
        var conta = new ContaBuilder().Build();
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        await _handler.HandleAsync(new AtualizarPreferenciaNotificacaoCommand(true));

        _contaRepo.Verify(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()), Times.Once);
        _contaRepo.Verify(r => r.ObterPorIdAsync(It.Is<Guid>(g => g != ContaId), It.IsAny<CancellationToken>()), Times.Never);
    }
}
