using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class VerificarOnboardingTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<VerificarOnboardingTreinadorHandler>> _logger = new();
    private readonly VerificarOnboardingTreinadorHandler _handler;

    public VerificarOnboardingTreinadorHandlerTests()
    {
        _handler = new VerificarOnboardingTreinadorHandler(
            _treinadorRepo.Object, _contaRecebimentoRepo.Object, _stripeService.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_SemContaStripe_RetornaFalse()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await _handler.HandleAsync(new VerificarOnboardingTreinadorQuery(treinador.Id));

        result.OnboardingCompleto.Should().BeFalse();
        result.ContaConfigurada.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_OnboardingJaCompleto_RetornaTrueSemChamarStripe()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);
        var contaRecebimento = ContaRecebimento.Criar(treinador.Id, DateTime.UtcNow);
        contaRecebimento.ConfigurarStripeConnect("acct_123");
        contaRecebimento.ConfirmarOnboarding();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(new VerificarOnboardingTreinadorQuery(treinador.Id));

        result.OnboardingCompleto.Should().BeTrue();
        result.ContaConfigurada.Should().BeTrue();
        _stripeService.Verify(s => s.ContaEstaAtivadaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaConfiguradaMasNaoAtivada_NaoConfirmaOnboarding()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);
        var contaRecebimento = ContaRecebimento.Criar(treinador.Id, DateTime.UtcNow);
        contaRecebimento.ConfigurarStripeConnect("acct_123");
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);
        _stripeService.Setup(s => s.ContaEstaAtivadaAsync("acct_123", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _handler.HandleAsync(new VerificarOnboardingTreinadorQuery(treinador.Id));

        result.OnboardingCompleto.Should().BeFalse();
        result.ContaConfigurada.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaAtivada_ConfirmaOnboarding()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);
        var contaRecebimento = ContaRecebimento.Criar(treinador.Id, DateTime.UtcNow);
        contaRecebimento.ConfigurarStripeConnect("acct_123");
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);
        _stripeService.Setup(s => s.ContaEstaAtivadaAsync("acct_123", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.HandleAsync(new VerificarOnboardingTreinadorQuery(treinador.Id));

        result.OnboardingCompleto.Should().BeTrue();
        result.ContaConfigurada.Should().BeTrue();
        contaRecebimento.OnboardingCompleto.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new VerificarOnboardingTreinadorQuery(Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }
}
