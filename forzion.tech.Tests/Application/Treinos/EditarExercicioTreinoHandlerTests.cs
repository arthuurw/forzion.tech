using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.EditarExercicioTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class EditarExercicioTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<EditarExercicioTreinoHandler>> _logger = new();
    private readonly EditarExercicioTreinoHandler _handler;

    private static readonly IReadOnlyList<SerieConfigEditCommand> SeriesValidas =
        [new SerieConfigEditCommand(4, 8, 12, "Trabalho", 20m, 90)];

    public EditarExercicioTreinoHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new EditarExercicioTreinoHandler(
            _treinoRepo.Object, _exercicioRepo.Object, _execucaoRepo.Object, _unitOfWork.Object, _userContext.Object, _logger.Object);
    }

    private static (Treino treino, TreinoExercicio exercicio) CriarTreinoComExercicio(Guid treinadorId)
    {
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);
        var ex = treino.AdicionarExercicio(Guid.NewGuid());
        ex.AdicionarSerie(3, 10, 12, null, null, null);
        return (treino, ex);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaSeriesERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var (treino, ex) = CriarTreinoComExercicio(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new EditarExercicioTreinoCommand(treino.Id, ex.Id, SeriesValidas);
        var result = await _handler.HandleAsync(command);

        result.Value.Exercicios.Should().HaveCount(1);
        result.Value.Exercicios[0].Series.Should().HaveCount(1);
        result.Value.Exercicios[0].Series[0].Quantidade.Should().Be(4);
        result.Value.Exercicios[0].Series[0].RepeticoesMin.Should().Be(8);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MultiplasSeries_SubstituiTodasAsSeries()
    {
        var treinadorId = Guid.NewGuid();
        var (treino, ex) = CriarTreinoComExercicio(treinadorId);
        ex.AdicionarSerie(2, 15, null, null, null, null);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        IReadOnlyList<SerieConfigEditCommand> novasSeries = [
            new SerieConfigEditCommand(3, 8, 10, null, null, null),
            new SerieConfigEditCommand(2, 12, null, "Drop", null, 30),
            new SerieConfigEditCommand(1, 20, null, null, null, null),
        ];
        var command = new EditarExercicioTreinoCommand(treino.Id, ex.Id, novasSeries);
        var result = await _handler.HandleAsync(command);

        result.Value.Exercicios[0].Series.Should().HaveCount(3);
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var (treino, ex) = CriarTreinoComExercicio(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(new EditarExercicioTreinoCommand(treino.Id, ex.Id, SeriesValidas));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_PodeEditar()
    {
        var (treino, ex) = CriarTreinoComExercicio(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _handler.HandleAsync(new EditarExercicioTreinoCommand(treino.Id, ex.Id, SeriesValidas));

        result.Value.Exercicios.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        _treinoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(
            new EditarExercicioTreinoCommand(Guid.NewGuid(), Guid.NewGuid(), SeriesValidas));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoJaExecutado_LancaTreinoExecutadoException()
    {
        var (treino, ex) = CriarTreinoComExercicio(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = async () => await _handler.HandleAsync(new EditarExercicioTreinoCommand(treino.Id, ex.Id, SeriesValidas));

        await act.Should().ThrowAsync<TreinoExecutadoException>();
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var (treino, _) = CriarTreinoComExercicio(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var idInexistente = Guid.NewGuid();
        var act = async () => await _handler.HandleAsync(
            new EditarExercicioTreinoCommand(treino.Id, idInexistente, SeriesValidas));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_SeriesVazias_LancaDomainException()
    {
        var (treino, ex) = CriarTreinoComExercicio(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = async () => await _handler.HandleAsync(new EditarExercicioTreinoCommand(treino.Id, ex.Id, []));
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
