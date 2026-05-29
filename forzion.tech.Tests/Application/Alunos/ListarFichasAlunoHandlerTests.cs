using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ListarFichasAlunoHandlerTests
{
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly ListarFichasAlunoHandler _handler;

    public ListarFichasAlunoHandlerTests()
    {
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new ListarFichasAlunoHandler(_treinoAlunoRepo.Object, _exercicioRepo.Object);
    }

    private static TreinoAlunoDetalhe CriarDetalhe(Guid alunoId)
    {
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.NewGuid(), DateTime.UtcNow).Value;
        var treinoAluno = TreinoAluno.Criar(treino.Id, alunoId, DateTime.UtcNow).Value;
        return new TreinoAlunoDetalhe(treinoAluno, treino);
    }

    [Fact]
    public async Task HandleAsync_SemFichas_RetornaListaVazia()
    {
        var alunoId = Guid.NewGuid();
        _treinoAlunoRepo.Setup(r => r.ListarDetalhesPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<TreinoAlunoDetalhe>(), 0));

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ComFichas_RetornaItemsMapeados()
    {
        var alunoId = Guid.NewGuid();
        var detalhes = new List<TreinoAlunoDetalhe> { CriarDetalhe(alunoId) };
        _treinoAlunoRepo.Setup(r => r.ListarDetalhesPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((detalhes, 1));

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Items[0].NomeTreino.Should().Be("Treino A");
        result.Items[0].Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
    }

    [Fact]
    public async Task HandleAsync_PaginacaoCorreta_RefleteNaResposta()
    {
        var alunoId = Guid.NewGuid();
        _treinoAlunoRepo.Setup(r => r.ListarDetalhesPorAlunoAsync(alunoId, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<TreinoAlunoDetalhe>(), 0));

        var result = await _handler.HandleAsync(alunoId, 2, 5);

        result.Pagina.Should().Be(2);
        result.TamanhoPagina.Should().Be(5);
    }
}
