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
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly ListarTreinosDoTreinadorHandler _handler;

    public ListarTreinosDoTreinadorHandlerTests()
    {
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new ListarTreinosDoTreinadorHandler(_treinoRepo.Object, _exercicioRepo.Object);
    }

    private static Treino CriarTreino(Guid treinadorId) =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);

    private static IReadOnlyList<(Treino Treino, string? NomeAluno)> EmptyItems() =>
        new List<(Treino, string?)>();

    private static IReadOnlyList<(Treino Treino, string? NomeAluno)> WrapTreinos(IEnumerable<Treino> treinos) =>
        treinos.Select(t => (t, (string?)null)).ToList();

    [Fact]
    public async Task HandleAsync_SemTreinos_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 10,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmptyItems(), 0));

        var result = await _handler.HandleAsync(treinadorId, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ComTreinos_RetornaItems()
    {
        var treinadorId = Guid.NewGuid();
        var treinos = new List<Treino> { CriarTreino(treinadorId), CriarTreino(treinadorId) };
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 10,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WrapTreinos(treinos), 2));

        var result = await _handler.HandleAsync(treinadorId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_PaginacaoCorreta_RefleteNaResposta()
    {
        var treinadorId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 3, 5,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmptyItems(), 0));

        var result = await _handler.HandleAsync(treinadorId, 3, 5);

        result.Pagina.Should().Be(3);
        result.TamanhoPagina.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_ComNomeAluno_RetornaNoResponse()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        var items = new List<(Treino, string?)> { (treino, "João Silva") };
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 10,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var result = await _handler.HandleAsync(treinadorId, 1, 10);

        result.Items[0].NomeAluno.Should().Be("João Silva");
    }

    [Fact]
    public async Task HandleAsync_SemAlunoVinculado_NomeAlunoNulo()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        var items = new List<(Treino, string?)> { (treino, null) };
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 10,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var result = await _handler.HandleAsync(treinadorId, 1, 10);

        result.Items[0].NomeAluno.Should().BeNull();
    }
}
