using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Domain.Entities;
using Moq;

namespace forzion.tech.Tests.Application.Admin.GruposMusculares;

public class ListarGruposMuscularesHandlerTests
{
    private readonly Mock<IGrupoMuscularRepository> _repository = new();
    private readonly ListarGruposMuscularesHandler _handler;

    public ListarGruposMuscularesHandlerTests()
    {
        _handler = new ListarGruposMuscularesHandler(_repository.Object);
    }

    [Fact]
    public async Task HandleAsync_ExistemGrupos_RetornaTodos()
    {
        var grupos = new List<GrupoMuscular>
        {
            GrupoMuscular.Criar("Peito"),
            GrupoMuscular.Criar("Costas"),
            GrupoMuscular.Criar("Pernas"),
        };
        _repository.Setup(r => r.ListarTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(grupos);

        var result = await _handler.HandleAsync();

        result.Should().HaveCount(3);
        result.Select(r => r.Nome).Should().BeEquivalentTo("Peito", "Costas", "Pernas");
    }

    [Fact]
    public async Task HandleAsync_SemGrupos_RetornaListaVazia()
    {
        _repository.Setup(r => r.ListarTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _handler.HandleAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MapeiaCamposCorretamente()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        _repository.Setup(r => r.ListarTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([grupo]);

        var result = await _handler.HandleAsync();

        result[0].Id.Should().Be(grupo.Id);
        result[0].Nome.Should().Be("Peito");
    }
}
