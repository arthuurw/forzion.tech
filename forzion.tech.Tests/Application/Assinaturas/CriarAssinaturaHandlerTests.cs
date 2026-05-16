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
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CriarAssinaturaHandler>> _logger = new();
    private readonly CriarAssinaturaHandler _handler;

    public CriarAssinaturaHandlerTests()
    {
        _handler = new CriarAssinaturaHandler(
            _assinaturaRepo.Object, _treinadorRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    private static CriarAssinaturaCommand BuildCommand(Guid treinadorId) => new(
        Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m);

    [Fact]
    public async Task HandleAsync_TreinadorComOnboarding_CriaAssinatura()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        treinador.ConfigurarStripeConnect("acct_123");
        treinador.ConfirmarOnboarding();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id));

        result.TreinadorId.Should().Be(treinador.Id);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Assinatura>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemOnboarding_LancaDomainException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(BuildCommand(treinador.Id));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*recebimentos*");
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(BuildCommand(Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
