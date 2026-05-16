using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.IniciarOnboarding;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class IniciarOnboardingTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<IniciarOnboardingTreinadorHandler>> _logger = new();
    private readonly IniciarOnboardingTreinadorHandler _handler;

    public IniciarOnboardingTreinadorHandlerTests()
    {
        _handler = new IniciarOnboardingTreinadorHandler(
            _treinadorRepo.Object, _contaRepo.Object, _stripeService.Object,
            _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemConta_CriaContaERetornaLink()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        var conta = Conta.Criar(Email.Criar("carlos@test.com"), "hash", TipoConta.Treinador);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _stripeService.Setup(s => s.CriarContaConnectAsync("carlos@test.com", "Carlos", It.IsAny<CancellationToken>()))
            .ReturnsAsync("acct_new");
        _stripeService.Setup(s => s.GerarLinkOnboardingAsync("acct_new", "https://ret", "https://cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://stripe.com/onboard");

        var result = await _handler.HandleAsync(new IniciarOnboardingTreinadorCommand(treinador.Id, "https://ret", "https://cancel"));

        result.Value.Should().Be("https://stripe.com/onboard");
        treinador.StripeConnectAccountId.Should().Be("acct_new");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorComContaExistente_NaoCriaNovaConta()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        treinador.ConfigurarStripeConnect("acct_existing");
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _stripeService.Setup(s => s.GerarLinkOnboardingAsync("acct_existing", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://stripe.com/onboard");

        var result = await _handler.HandleAsync(new IniciarOnboardingTreinadorCommand(treinador.Id, "https://ret", "https://cancel"));

        result.Value.Should().Be("https://stripe.com/onboard");
        _stripeService.Verify(s => s.CriarContaConnectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new IniciarOnboardingTreinadorCommand(Guid.NewGuid(), "ret", "cancel"));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
