using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Planos.ListarPlanosPlataforma;
using forzion.tech.Domain.Entities;
using Moq;

namespace forzion.tech.Tests.Application.Planos;

public class ListarPlanosPlataformaHandlerTests
{
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly ListarPlanosPlataformaHandler _handler;

    public ListarPlanosPlataformaHandlerTests()
    {
        _handler = new ListarPlanosPlataformaHandler(_planoRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_ExistemPlanos_RetornaTodos()
    {
        var planos = new List<PlanoPlataforma>
        {
            PlanoPlataforma.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 5, 49.90m, DateTime.UtcNow),
            PlanoPlataforma.Criar("Pro", forzion.tech.Domain.Enums.TierPlano.Pro, 20, 149.90m, DateTime.UtcNow),
        };
        _planoRepo.Setup(r => r.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(planos);

        var result = await _handler.HandleAsync();

        result.Should().HaveCount(2);
        result.Select(p => p.Nome).Should().BeEquivalentTo("Starter", "Pro");
    }

    [Fact]
    public async Task HandleAsync_SemPlanos_RetornaListaVazia()
    {
        _planoRepo.Setup(r => r.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _handler.HandleAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MapeiaCamposCorretamente()
    {
        var plano = PlanoPlataforma.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m, DateTime.UtcNow);
        _planoRepo.Setup(r => r.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([plano]);

        var result = await _handler.HandleAsync();

        result[0].PlanoId.Should().Be(plano.Id);
        result[0].Nome.Should().Be("Starter");
        result[0].MaxAlunos.Should().Be(10);
        result[0].Preco.Should().Be(99.90m);
    }
}
