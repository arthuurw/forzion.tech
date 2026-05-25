using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ListarFichasDoAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ListarFichasDoAlunoHandlerTests
{
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly ListarFichasDoAlunoHandler _handler;

    public ListarFichasDoAlunoHandlerTests()
    {
        _handler = new ListarFichasDoAlunoHandler(_treinoAlunoRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_SemFichas_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        _treinoAlunoRepo.Setup(r => r.ListarAtivosComNomePorParAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreinoAlunoComNome>());

        var result = await _handler.HandleAsync(treinadorId, alunoId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ComFichas_RetornaItemsMapeados()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treinoId = Guid.NewGuid();
        var treinoAluno = TreinoAluno.Criar(treinoId, alunoId, DateTime.UtcNow);
        var fichas = new List<TreinoAlunoComNome>
        {
            new(treinoAluno, "Treino Força")
        };
        _treinoAlunoRepo.Setup(r => r.ListarAtivosComNomePorParAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fichas);

        var result = await _handler.HandleAsync(treinadorId, alunoId);

        result.Should().HaveCount(1);
        result[0].TreinoAlunoId.Should().Be(treinoAluno.Id);
        result[0].TreinoId.Should().Be(treinoId);
        result[0].NomeTreino.Should().Be("Treino Força");
        result[0].Status.Should().Be(TreinoAlunoStatus.Ativo.ToString());
    }

    [Fact]
    public async Task HandleAsync_MultiplasFichas_RetornaTodas()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var fichas = new List<TreinoAlunoComNome>
        {
            new(TreinoAluno.Criar(Guid.NewGuid(), alunoId, DateTime.UtcNow), "Treino A"),
            new(TreinoAluno.Criar(Guid.NewGuid(), alunoId, DateTime.UtcNow), "Treino B"),
            new(TreinoAluno.Criar(Guid.NewGuid(), alunoId, DateTime.UtcNow), "Treino C")
        };
        _treinoAlunoRepo.Setup(r => r.ListarAtivosComNomePorParAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fichas);

        var result = await _handler.HandleAsync(treinadorId, alunoId);

        result.Should().HaveCount(3);
        result.Select(f => f.NomeTreino).Should().BeEquivalentTo("Treino A", "Treino B", "Treino C");
    }
}
