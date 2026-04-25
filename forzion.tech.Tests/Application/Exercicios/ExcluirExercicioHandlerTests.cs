using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using GrupoMuscular = forzion.tech.Domain.Enums.GrupoMuscular;
using Moq;

namespace forzion.tech.Tests.Application.Exercicios;

public class ExcluirExercicioHandlerTests
{
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ExcluirExercicioHandler _handler;

    public ExcluirExercicioHandlerTests()
    {
        _handler = new ExcluirExercicioHandler(_exercicioRepo.Object, _unitOfWork.Object);
    }

    private static Exercicio CriarExercicioTreinador(Guid treinadorId) =>
        Exercicio.Criar("Supino Reto", GrupoMuscular.Peito, treinadorId);

    private static Exercicio CriarExercicioGlobal() =>
        Exercicio.Criar("Agachamento", GrupoMuscular.Pernas);

    [Fact]
    public async Task HandleAsync_TreinadorExcluiProprio_RemoveEComita()
    {
        var treinadorId = Guid.NewGuid();
        var exercicio = CriarExercicioTreinador(treinadorId);
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.EstaEmUsoAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, treinadorId));

        _exercicioRepo.Verify(r => r.RemoverAsync(exercicio, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminExcluiGlobal_RemoveEComita()
    {
        var exercicio = CriarExercicioGlobal();
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.EstaEmUsoAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, null));

        _exercicioRepo.Verify(r => r.RemoverAsync(exercicio, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoEncontrado_LancaExercicioNaoEncontradoException()
    {
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Exercicio?)null);

        var act = async () => await _handler.HandleAsync(new ExcluirExercicioCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<ExercicioNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorExcluiDeOutro_LancaAcessoNegadoException()
    {
        var exercicio = CriarExercicioTreinador(Guid.NewGuid());
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);

        var act = async () => await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AdminExcluiNaoGlobal_LancaAcessoNegadoException()
    {
        var exercicio = CriarExercicioTreinador(Guid.NewGuid());
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);

        var act = async () => await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, null));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_ExercicioEmUso_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var exercicio = CriarExercicioTreinador(treinadorId);
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.EstaEmUsoAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = async () => await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, treinadorId));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*em uso*");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
