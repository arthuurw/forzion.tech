using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadores;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class ListarTreinadoresHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly ListarTreinadoresHandler _handler;

    public ListarTreinadoresHandlerTests()
    {
        _handler = new ListarTreinadoresHandler(_treinadorRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_SemFiltroStatus_RetornaListaPaginada()
    {
        var treinadores = new List<Treinador>
        {
            CriarTreinador("Ana"), CriarTreinador("Bruno")
        };
        _treinadorRepo.Setup(r => r.ListarAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((treinadores, 2));

        var result = await _handler.HandleAsync(null, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }

    [Fact]
    public async Task HandleAsync_ComFiltroStatus_PassaFiltroAoRepositorio()
    {
        _treinadorRepo.Setup(r => r.ListarAsync(TreinadorStatus.Ativo, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Treinador>(), 0));

        var result = await _handler.HandleAsync(TreinadorStatus.Ativo, 1, 10);

        result.Total.Should().Be(0);
        _treinadorRepo.Verify(r => r.ListarAsync(TreinadorStatus.Ativo, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ListaVazia_RetornaRespostaVazia()
    {
        _treinadorRepo.Setup(r => r.ListarAsync(It.IsAny<TreinadorStatus?>(), 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Treinador>(), 0));

        var result = await _handler.HandleAsync(null, 1, 10);

        result.Items.Should().BeEmpty();
    }

    private static Treinador CriarTreinador(string nome) =>
        Treinador.Criar(Guid.NewGuid(), nome, DateTime.UtcNow).Value;
}
