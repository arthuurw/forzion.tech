using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ListarExerciciosHandlerTests
{
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<ILogger<ListarExerciciosHandler>> _logger = new();
    private readonly ListarExerciciosHandler _handler;

    public ListarExerciciosHandlerTests()
    {
        _handler = new ListarExerciciosHandler(_exercicioRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_ComExercicios_RetornaListaPaginada()
    {
        var treinadorId = Guid.NewGuid();
        var exercicios = new List<Exercicio>
        {
            Exercicio.Criar("Supino Reto", forzion.tech.Domain.Enums.GrupoMuscular.Peito, treinadorId),
            Exercicio.Criar("Agachamento", forzion.tech.Domain.Enums.GrupoMuscular.Pernas, treinadorId)
        };

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)exercicios, 2));

        var result = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorId, 1, 10));

        result.Items.Should().HaveCount(2);
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
        _exercicioRepo.Verify(r => r.ListarAsync(outroTreinadorId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<forzion.tech.Domain.Enums.GrupoMuscular?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorIdDiferente_NaoRetornaExerciciosDeOutroTreinador()
    {
        var treinadorA = Guid.NewGuid();
        var treinadorB = Guid.NewGuid();

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorA, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[Exercicio.Criar("Supino", forzion.tech.Domain.Enums.GrupoMuscular.Peito, treinadorA)], 1));

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorB, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[], 0));

        var resultA = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorA, 1, 10));
        var resultB = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorB, 1, 10));

        resultA.Items.Should().HaveCount(1);
        resultB.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_IncluiExerciciosGlobais_JuntoComDoTreinador()
    {
        var treinadorId = Guid.NewGuid();
        var exercicios = new List<Exercicio>
        {
            Exercicio.Criar("Supino Reto", forzion.tech.Domain.Enums.GrupoMuscular.Peito, treinadorId),
            Exercicio.Criar("Agachamento Livre", forzion.tech.Domain.Enums.GrupoMuscular.Pernas, null)
        };

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>(), null, null, "nome"))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)exercicios, 2));

        var result = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorId, 1, 10));

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
