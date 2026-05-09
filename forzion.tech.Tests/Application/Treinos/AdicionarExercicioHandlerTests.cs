using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class AdicionarExercicioHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<AdicionarExercicioHandler>> _logger = new();
    private readonly AdicionarExercicioCommandValidator _validator = new();
    private readonly AdicionarExercicioHandler _handler;

    private static readonly IReadOnlyList<SerieConfigCommand> SerieValida =
        [new SerieConfigCommand(3, 10, 12, null, 50m, 60)];

    public AdicionarExercicioHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _handler = new AdicionarExercicioHandler(
            _treinoRepo.Object,
            _exercicioRepo.Object,
            _execucaoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            _validator,
            _logger.Object);
    }

    private static Treino CriarTreino(Guid treinadorId) =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId);

    [Fact]
    public async Task HandleAsync_DadosValidos_AdicionaExercicioERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        var exercicioId = Guid.NewGuid();

        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _exercicioRepo.Setup(r => r.ExisteAsync(exercicioId, treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new AdicionarExercicioCommand(treino.Id, exercicioId, SerieValida);
        var result = await _handler.HandleAsync(command);

        result.Exercicios.Should().HaveCount(1);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AcessoNegado_LancaAcessoNegadoException()
    {
        var treinadorId = Guid.NewGuid();
        var outroTreinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(outroTreinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AdicionarExercicioCommand(treino.Id, Guid.NewGuid(), SerieValida);
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var command = new AdicionarExercicioCommand(treinoId, Guid.NewGuid(), SerieValida);
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoJaExecutado_LancaTreinoExecutadoException()
    {
        var treino = CriarTreino(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new AdicionarExercicioCommand(treino.Id, Guid.NewGuid(), SerieValida);
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<TreinoExecutadoException>();
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoEncontrado_LancaExercicioNaoEncontradoException()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        var exercicioId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _exercicioRepo.Setup(r => r.ExisteAsync(exercicioId, treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new AdicionarExercicioCommand(treino.Id, exercicioId, SerieValida);
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ExercicioNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_SeriesVazias_LancaValidationException()
    {
        var command = new AdicionarExercicioCommand(Guid.NewGuid(), Guid.NewGuid(), []);
        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_ExercicioIdVazio_LancaValidationException()
    {
        var command = new AdicionarExercicioCommand(Guid.NewGuid(), Guid.Empty, SerieValida);
        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
