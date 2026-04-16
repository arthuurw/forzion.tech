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
        var tenantId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var treinos = new List<Treino>
        {
            Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tenantId, treinadorId),
            Treino.Criar("Treino B", ObjetivoTreino.Forca, tenantId, treinadorId)
        };

        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(tenantId, alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)treinos, 2));

        var result = await _handler.HandleAsync(new ListarTreinosQuery(tenantId, alunoId, 1, 10));

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_SemTreinos_RetornaListaVazia()
    {
        var tenantId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(tenantId, alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[], 0));

        var result = await _handler.HandleAsync(new ListarTreinosQuery(tenantId, alunoId, 1, 10));

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
