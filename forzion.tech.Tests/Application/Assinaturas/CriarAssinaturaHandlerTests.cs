using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Assinaturas.CriarAssinatura;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Assinaturas;

public class CriarAssinaturaHandlerTests
{
    private readonly Mock<IAssinaturaRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CriarAssinaturaHandler>> _logger = new();
    private readonly CriarAssinaturaHandler _handler;

    public CriarAssinaturaHandlerTests()
    {
        _handler = new CriarAssinaturaHandler(
            _assinaturaRepo.Object, _contaRecebimentoRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    private static CriarAssinaturaCommand BuildCommand(Guid treinadorId) => new(
        Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m);

    private static ContaRecebimento ContaOnboarded(Guid treinadorId)
    {
        var conta = ContaRecebimento.Criar(treinadorId);
        conta.ConfigurarStripeConnect("acct_123");
        conta.ConfirmarOnboarding();
        return conta;
    }

    [Fact]
    public async Task HandleAsync_TreinadorComOnboarding_CriaAssinatura()
    {
        var treinadorId = Guid.NewGuid();
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinadorId));

        var result = await _handler.HandleAsync(BuildCommand(treinadorId));

        result.TreinadorId.Should().Be(treinadorId);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Assinatura>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ContaRecebimentoSemOnboarding_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var conta = ContaRecebimento.Criar(treinadorId);
        conta.ConfigurarStripeConnect("acct_123");
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var act = async () => await _handler.HandleAsync(BuildCommand(treinadorId));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*recebimentos*");
    }

    [Fact]
    public async Task HandleAsync_SemContaRecebimento_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var act = async () => await _handler.HandleAsync(BuildCommand(treinadorId));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*recebimentos*");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
