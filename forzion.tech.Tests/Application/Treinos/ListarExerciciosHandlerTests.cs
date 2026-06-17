using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ListarExerciciosHandlerTests
{
    private static readonly Guid GrupoId = Guid.NewGuid();

    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IGrupoMuscularRepository> _grupoRepo = new();
    private readonly Mock<ILogger<ListarExerciciosHandler>> _logger = new();
    private readonly ListarExerciciosHandler _handler;

    public ListarExerciciosHandlerTests()
    {
        _grupoRepo.Setup(r => r.ListarTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GrupoMuscular>)[]);
        _handler = new ListarExerciciosHandler(_exercicioRepo.Object, _grupoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_ComExercicios_RetornaListaPaginada()
    {
        var treinadorId = Guid.NewGuid();
        var supino = Exercicio.Criar("Supino Reto", GrupoId, DateTime.UtcNow, treinadorId).Value;
        var agachamento = Exercicio.Criar("Agachamento", GrupoId, DateTime.UtcNow, treinadorId).Value;
        var exercicios = new List<Exercicio> { supino, agachamento };

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)exercicios, 2));

        var result = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorId, 1, 10));

        result.Items.Select(i => i.ExercicioId).Should().Equal(supino.Id, agachamento.Id);
        result.Items.Select(i => i.Nome).Should().Equal("Supino Reto", "Agachamento");
        result.Total.Should().Be(2);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_SemExercicios_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[], 0));

        var result = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorId, 1, 10));

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_PassaTreinadorIdCorretoAoRepositorio_IsolamentoGarantido()
    {
        var treinadorId = Guid.NewGuid();
        var outroTreinadorId = Guid.NewGuid();

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[], 0));

        await _handler.HandleAsync(new ListarExerciciosQuery(treinadorId, 1, 10));

        _exercicioRepo.Verify(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"), Times.Once);
        _exercicioRepo.Verify(r => r.ListarAsync(outroTreinadorId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorIdDiferente_NaoRetornaExerciciosDeOutroTreinador()
    {
        var treinadorA = Guid.NewGuid();
        var treinadorB = Guid.NewGuid();
        var exercicioDeA = Exercicio.Criar("Supino", GrupoId, DateTime.UtcNow, treinadorA).Value;

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorA, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[exercicioDeA], 1));

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorB, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[], 0));

        var resultA = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorA, 1, 10));
        var resultB = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorB, 1, 10));

        resultA.Items.Select(i => i.ExercicioId).Should().ContainSingle().Which.Should().Be(exercicioDeA.Id);
        resultB.Items.Select(i => i.ExercicioId).Should().NotContain(exercicioDeA.Id);
    }

    [Fact]
    public async Task HandleAsync_IncluiExerciciosGlobais_JuntoComDoTreinador()
    {
        var treinadorId = Guid.NewGuid();
        var doTreinador = Exercicio.Criar("Supino Reto", GrupoId, DateTime.UtcNow, treinadorId).Value;
        var global = Exercicio.Criar("Agachamento Livre", GrupoId, DateTime.UtcNow, null).Value;

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[doTreinador, global], 2));

        var result = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorId, 1, 10));

        result.Items.Should().SatisfyRespectively(
            primeiro =>
            {
                primeiro.ExercicioId.Should().Be(doTreinador.Id);
                primeiro.TreinadorId.Should().Be(treinadorId);
                primeiro.IsGlobal.Should().BeFalse();
            },
            segundo =>
            {
                segundo.ExercicioId.Should().Be(global.Id);
                segundo.TreinadorId.Should().BeNull();
                segundo.IsGlobal.Should().BeTrue();
            });
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
