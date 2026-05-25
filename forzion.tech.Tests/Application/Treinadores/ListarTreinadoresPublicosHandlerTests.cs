using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadoresPublicos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class ListarTreinadoresPublicosHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly ListarTreinadoresPublicosHandler _handler;

    public ListarTreinadoresPublicosHandlerTests()
    {
        _handler = new ListarTreinadoresPublicosHandler(_treinadorRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadoresAtivos_RetornaListaMapeada()
    {
        var treinadores = new List<Treinador>
        {
            CriarTreinador("Ana"), CriarTreinador("Bruno")
        };
        _treinadorRepo.Setup(r => r.ListarAtivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinadores);

        var result = await _handler.HandleAsync();

        result.Should().HaveCount(2);
        result.Select(t => t.Nome).Should().Contain(["Ana", "Bruno"]);
    }

    [Fact]
    public async Task HandleAsync_SemTreinadoresAtivos_RetornaListaVazia()
    {
        _treinadorRepo.Setup(r => r.ListarAtivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Treinador>());

        var result = await _handler.HandleAsync();

        result.Should().BeEmpty();
    }

    private static Treinador CriarTreinador(string nome) =>
        Treinador.Criar(Guid.NewGuid(), nome, DateTime.UtcNow);
}
