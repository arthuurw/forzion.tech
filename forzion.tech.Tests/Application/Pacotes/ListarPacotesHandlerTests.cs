using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotes;
using forzion.tech.Domain.Entities;
using Moq;

namespace forzion.tech.Tests.Application.Pacotes;

public class ListarPacotesHandlerTests
{
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly ListarPacotesHandler _handler;

    public ListarPacotesHandlerTests()
    {
        _handler = new ListarPacotesHandler(_pacoteRepo.Object);
    }

    private static Pacote CriarPacote(Guid treinadorId) =>
        Pacote.Criar(treinadorId, "Pacote Teste", 200m, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_SemPacotes_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Pacote>());

        var result = await _handler.HandleAsync(treinadorId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ComPacotes_RetornaItemsMapeados()
    {
        var treinadorId = Guid.NewGuid();
        var pacotes = new List<Pacote> { CriarPacote(treinadorId), CriarPacote(treinadorId) };
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
            .ReturnsAsync(new List<Pacote> { CriarPacote(treinadorA) });
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Pacote>());

        var result = await _handler.HandleAsync(treinadorB);

        result.Should().BeEmpty();
    }
}
