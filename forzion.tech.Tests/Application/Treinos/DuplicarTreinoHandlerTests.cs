using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.DuplicarTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class DuplicarTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<DuplicarTreinoHandler>> _logger = new();
    private readonly DuplicarTreinoHandler _handler;

    public DuplicarTreinoHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new DuplicarTreinoHandler(
            _treinoRepo.Object,
            _exercicioRepo.Object,
            _unitOfWork.Object,
            _userContext.Object, TimeProvider.System,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinoExistente_DuplicaERetornaCopia()
    {
        var treinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;
        var ex = treino.AdicionarExercicio(Guid.NewGuid(), DateTime.UtcNow).Value;
        ex.AdicionarSerie(3, 12, null, null, 50m, 60);

        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var result = await _handler.HandleAsync(new DuplicarTreinoCommand(treinadorId, treino.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.TreinoId.Should().NotBe(treino.Id);
        result.Value.Nome.Should().Be("Treino A (cópia)");
        result.Value.Exercicios.Should().HaveCount(1);
        _treinoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treino>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AcessoNegado_LancaAcessoNegadoException()
    {
        var treinadorId = Guid.NewGuid();
        var outroTreinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(outroTreinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new DuplicarTreinoCommand(outroTreinadorId, treino.Id);
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new DuplicarTreinoCommand(Guid.NewGuid(), treinoId));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
