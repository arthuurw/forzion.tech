using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;
using forzion.tech.Domain.Entities;
using Moq;

namespace forzion.tech.Tests.Application.Pacotes;

public class ListarPacotesAlunoHandlerTests
{
    private readonly Mock<IPacoteAlunoRepository> _pacoteRepo = new();
    private readonly ListarPacotesAlunoHandler _handler;

    public ListarPacotesAlunoHandlerTests()
    {
        _handler = new ListarPacotesAlunoHandler(_pacoteRepo.Object);
    }

    private static PacoteAluno CriarPacote(Guid treinadorId) =>
        PacoteAluno.Criar(treinadorId, "Pacote Teste", 200m);

    [Fact]
    public async Task HandleAsync_SemPacotes_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PacoteAluno>());

        var result = await _handler.HandleAsync(treinadorId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ComPacotes_RetornaItemsMapeados()
    {
        var treinadorId = Guid.NewGuid();
        var pacotes = new List<PacoteAluno> { CriarPacote(treinadorId), CriarPacote(treinadorId) };
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacotes);

        var result = await _handler.HandleAsync(treinadorId);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.TreinadorId.Should().Be(treinadorId));
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_NaoRetornaPacotesDeOutro()
    {
        var treinadorA = Guid.NewGuid();
        var treinadorB = Guid.NewGuid();
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PacoteAluno> { CriarPacote(treinadorA) });
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PacoteAluno>());

        var result = await _handler.HandleAsync(treinadorB);

        result.Should().BeEmpty();
    }
}
