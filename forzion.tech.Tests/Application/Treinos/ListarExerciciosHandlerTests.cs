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
            Exercicio.Criar("Supino Reto", GrupoMuscular.Peito, treinadorId),
            Exercicio.Criar("Agachamento", GrupoMuscular.Pernas, treinadorId)
        };

        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>()))
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
        _exercicioRepo.Setup(r => r.ListarAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Exercicio>)[], 0));

        var result = await _handler.HandleAsync(new ListarExerciciosQuery(treinadorId, 1, 10));

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
