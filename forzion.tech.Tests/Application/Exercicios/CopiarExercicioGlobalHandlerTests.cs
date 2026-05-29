using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.CopiarExercicioGlobal;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Exercicios;

public class CopiarExercicioGlobalHandlerTests
{
    private static readonly Guid GrupoId = Guid.NewGuid();

    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IGrupoMuscularRepository> _grupoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CopiarExercicioGlobalHandler>> _logger = new();
    private readonly CopiarExercicioGlobalHandler _handler;

    public CopiarExercicioGlobalHandlerTests()
    {
        _grupoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GrupoMuscular.Criar("Peito", DateTime.UtcNow).Value);
        _handler = new CopiarExercicioGlobalHandler(
            _exercicioRepo.Object, _grupoRepo.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_ExercicioGlobal_CriaCopiaNaBibliotecaDoTreinador()
    {
        var global = Exercicio.Criar("Supino", GrupoId, DateTime.UtcNow, null, "desc").Value;
        var treinadorId = Guid.NewGuid();

        _exercicioRepo.Setup(r => r.ObterPorIdAsync(global.Id, It.IsAny<CancellationToken>())).ReturnsAsync(global);

        var result = await _handler.HandleAsync(new CopiarExercicioGlobalCommand(global.Id, treinadorId));

        result.Nome.Should().Be("Supino");
        result.TreinadorId.Should().Be(treinadorId);
        result.IsGlobal.Should().BeFalse();
        result.Descricao.Should().Be("desc");
        _exercicioRepo.Verify(r => r.AdicionarAsync(It.IsAny<Exercicio>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoEncontrado_LancaException()
    {
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Exercicio?)null);

        var act = async () => await _handler.HandleAsync(new CopiarExercicioGlobalCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<ExercicioNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoGlobal_LancaAcessoNegado()
    {
        var proprio = Exercicio.Criar("Supino", GrupoId, DateTime.UtcNow, Guid.NewGuid()).Value;
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(proprio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proprio);

        var act = async () => await _handler.HandleAsync(new CopiarExercicioGlobalCommand(proprio.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }
}
