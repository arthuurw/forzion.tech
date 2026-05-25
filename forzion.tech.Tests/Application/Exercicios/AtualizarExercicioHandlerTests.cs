using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Exercicios;

public class AtualizarExercicioHandlerTests
{
    private static readonly Guid GrupoId = Guid.NewGuid();

    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IGrupoMuscularRepository> _grupoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly AtualizarExercicioHandler _handler;

    public AtualizarExercicioHandlerTests()
    {
        _grupoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GrupoMuscular.Criar("Peito", DateTime.UtcNow));
        _handler = new AtualizarExercicioHandler(_exercicioRepo.Object, _grupoRepo.Object, _unitOfWork.Object);
    }

    private static Exercicio CriarExercicioTreinador(Guid treinadorId) =>
        Exercicio.Criar("Supino Reto", GrupoId, DateTime.UtcNow, treinadorId);

    private static Exercicio CriarExercicioGlobal() =>
        Exercicio.Criar("Agachamento", GrupoId, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_TreinadorAtualizaProprio_RetornaResponse()
    {
        var treinadorId = Guid.NewGuid();
        var exercicio = CriarExercicioTreinador(treinadorId);
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.NomeJaExisteAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new AtualizarExercicioCommand(exercicio.Id, treinadorId, "Supino Inclinado", null, null);
        var result = await _handler.HandleAsync(command);

        result.Value.Nome.Should().Be("Supino Inclinado");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminAtualizaGlobal_RetornaResponse()
    {
        var exercicio = CriarExercicioGlobal();
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.NomeJaExisteAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new AtualizarExercicioCommand(exercicio.Id, null, "Agachamento Livre", null, null);
        var result = await _handler.HandleAsync(command);

        result.Value.Nome.Should().Be("Agachamento Livre");
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoEncontrado_LancaExercicioNaoEncontradoException()
    {
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Exercicio?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarExercicioCommand(Guid.NewGuid(), Guid.NewGuid(), "X", null, null));
        await act.Should().ThrowAsync<ExercicioNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorAtualizaDeOutro_LancaAcessoNegadoException()
    {
        var exercicio = CriarExercicioTreinador(Guid.NewGuid());
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);

        var act = async () => await _handler.HandleAsync(new AtualizarExercicioCommand(exercicio.Id, Guid.NewGuid(), "X", null, null));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AdminAtualizaNaoGlobal_LancaAcessoNegadoException()
    {
        var exercicio = CriarExercicioTreinador(Guid.NewGuid());
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);

        var act = async () => await _handler.HandleAsync(new AtualizarExercicioCommand(exercicio.Id, null, "X", null, null));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_NomeDuplicado_RetornaFalha()
    {
        var treinadorId = Guid.NewGuid();
        var exercicio = CriarExercicioTreinador(treinadorId);
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.NomeJaExisteAsync("Rosca Direta", treinadorId, exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.HandleAsync(new AtualizarExercicioCommand(exercicio.Id, treinadorId, "Rosca Direta", null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("nome");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
