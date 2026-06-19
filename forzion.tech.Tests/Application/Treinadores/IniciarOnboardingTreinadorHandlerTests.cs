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
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class IniciarOnboardingTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<IniciarOnboardingTreinadorHandler>> _logger = new();
    private readonly IniciarOnboardingTreinadorHandler _handler;

    public IniciarOnboardingTreinadorHandlerTests()
    {
        _handler = new IniciarOnboardingTreinadorHandler(
            _treinadorRepo.Object, _contaRecebimentoRepo.Object, _contaRepo.Object,
            _stripeService.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemConta_CriaContaERetornaLink()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var conta = Conta.Criar(Email.Criar("carlos@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _stripeService.Setup(s => s.CriarContaConnectAsync("carlos@test.com", "Carlos", It.IsAny<CancellationToken>()))
            .ReturnsAsync("acct_new");
        _stripeService.Setup(s => s.GerarLinkOnboardingAsync("acct_new", "https://ret", "https://cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://stripe.com/onboard");

        ContaRecebimento? adicionada = null;
        _contaRecebimentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<ContaRecebimento>(), It.IsAny<CancellationToken>()))
            .Callback<ContaRecebimento, CancellationToken>((c, _) => adicionada = c);

        var result = await _handler.HandleAsync(new IniciarOnboardingTreinadorCommand(treinador.Id, "https://ret", "https://cancel"));

        result.Value.Should().Be("https://stripe.com/onboard");
        adicionada.Should().NotBeNull();
        adicionada!.TreinadorId.Should().Be(treinador.Id);
        adicionada.StripeConnectAccountId.Should().Be("acct_new");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorComContaExistente_NaoCriaNovaConta()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var contaRecebimento = ContaRecebimento.Criar(treinador.Id, DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_existing", TestData.Agora);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);
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
    public async Task HandleAsync_ContaDoTreinadorNaoEncontrada_LancaEstadoInconsistente()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var act = async () => await _handler.HandleAsync(new IniciarOnboardingTreinadorCommand(treinador.Id, "https://ret", "https://cancel"));
        await act.Should().ThrowAsync<EstadoInconsistenteException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
