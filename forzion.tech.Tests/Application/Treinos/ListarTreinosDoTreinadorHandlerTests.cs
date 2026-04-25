using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ListarTreinosDoTreinadorHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly ListarTreinosDoTreinadorHandler _handler;

    public ListarTreinosDoTreinadorHandlerTests()
    {
        _handler = new ListarTreinosDoTreinadorHandler(_treinoRepo.Object);
    }

    private static Treino CriarTreino(Guid treinadorId) =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId);

    [Fact]
    public async Task HandleAsync_SemTreinos_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Treino>(), 0));

        var result = await _handler.HandleAsync(treinadorId, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ComTreinos_RetornaItems()
    {
        var treinadorId = Guid.NewGuid();
        var treinos = new List<Treino> { CriarTreino(treinadorId), CriarTreino(treinadorId) };
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((treinos, 2));

        var result = await _handler.HandleAsync(treinadorId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_PaginacaoCorreta_RefleteNaResposta()
    {
        var treinadorId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 3, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Treino>(), 0));

        var result = await _handler.HandleAsync(treinadorId, 3, 5);

        result.Pagina.Should().Be(3);
        result.TamanhoPagina.Should().Be(5);
    }
}
