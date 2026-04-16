using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.RemoverExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class RemoverExercicioHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<RemoverExercicioHandler>> _logger = new();
    private readonly RemoverExercicioHandler _handler;

    public RemoverExercicioHandlerTests()
    {
        _handler = new RemoverExercicioHandler(
            _treinoRepo.Object, _execucaoRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_ExercicioExistente_RemoveERetorna()
    {
        var tenantId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tenantId, Guid.NewGuid());
        treino.AdicionarExercicio(Guid.NewGuid(), 3, 12, null, null);
        var treinoExercicioId = treino.Exercicios[0].Id;

        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _handler.HandleAsync(new RemoverExercicioCommand(tenantId, treino.Id, treinoExercicioId));

        result.Exercicios.Should().BeEmpty();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var tenantId = Guid.NewGuid();
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new RemoverExercicioCommand(tenantId, treinoId, Guid.NewGuid()));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TenantDiferente_LancaAcessoNegadoException()
    {
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.NewGuid(), Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(new RemoverExercicioCommand(Guid.NewGuid(), treino.Id, Guid.NewGuid()));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoJaExecutado_LancaTreinoExecutadoException()
    {
        var tenantId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tenantId, Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = async () => await _handler.HandleAsync(new RemoverExercicioCommand(tenantId, treino.Id, Guid.NewGuid()));

        await act.Should().ThrowAsync<TreinoExecutadoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
