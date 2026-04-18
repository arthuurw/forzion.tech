using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ListarTreinosHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ILogger<ListarTreinosHandler>> _logger = new();
    private readonly ListarTreinosHandler _handler;

    public ListarTreinosHandlerTests()
    {
        _handler = new ListarTreinosHandler(_treinoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_ComTreinos_RetornaListaPaginada()
    {
        var alunoId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var treinos = new List<Treino>
        {
            Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId),
            Treino.Criar("Treino B", ObjetivoTreino.Forca, treinadorId)
        };

        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)treinos, 2));

        var result = await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_SemTreinos_RetornaListaVazia()
    {
        var alunoId = Guid.NewGuid();

        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[], 0));

        var result = await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_PassaAlunoIdCorretoAoRepositorio_IsolamentoGarantido()
    {
        var alunoId = Guid.NewGuid();
        var outroAlunoId = Guid.NewGuid();

        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[], 0));

        await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        _treinoRepo.Verify(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
        _treinoRepo.Verify(r => r.ListarPorAlunoAsync(outroAlunoId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoIdDiferente_NaoRetornaTreinosDeOutroAluno()
    {
        var alunoA = Guid.NewGuid();
        var alunoB = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();

        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoA, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId)], 1));

        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoB, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[], 0));

        var resultA = await _handler.HandleAsync(new ListarTreinosQuery(alunoA, 1, 10));
        var resultB = await _handler.HandleAsync(new ListarTreinosQuery(alunoB, 1, 10));

        resultA.Items.Should().HaveCount(1);
        resultB.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
