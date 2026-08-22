using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.ListarLeads;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class ListarLeadsHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly ListarLeadsHandler _handler;

    public ListarLeadsHandlerTests()
    {
        _handler = new ListarLeadsHandler(_leadRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_RepassaTodosOsFiltrosDaQueryAoRepositorio()
    {
        var treinadorId = Guid.NewGuid();
        var inicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        _leadRepo.Setup(r => r.ListarAsync(
                treinadorId, 2, 10, LeadStatus.EmContato, LeadSource.Agent, inicio, fim, "maria",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LeadListItem>(), 0));

        var query = new ListarLeadsQuery(treinadorId, 2, 10, LeadStatus.EmContato, LeadSource.Agent, inicio, fim, "maria");
        await _handler.HandleAsync(query);

        _leadRepo.Verify(r => r.ListarAsync(
            treinadorId, 2, 10, LeadStatus.EmContato, LeadSource.Agent, inicio, fim, "maria",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MapeiaItensETotalDoRepositorioParaResponse()
    {
        var treinadorId = Guid.NewGuid();
        var item = new LeadListItem(Guid.NewGuid(), "Fulano", TipoContatoLead.Email, "fulano@x.com", LeadSource.Agent, LeadStatus.Novo, DateTime.UtcNow, false);
        _leadRepo.Setup(r => r.ListarAsync(
                treinadorId, 1, 20, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LeadListItem> { item }, 1));

        var result = await _handler.HandleAsync(new ListarLeadsQuery(treinadorId));

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(item);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }

    [Fact]
    public async Task HandleAsync_SemResultados_RetornaListaVaziaComTotalZero()
    {
        var treinadorId = Guid.NewGuid();
        _leadRepo.Setup(r => r.ListarAsync(
                treinadorId, 1, 20, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LeadListItem>(), 0));

        var result = await _handler.HandleAsync(new ListarLeadsQuery(treinadorId));

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }
}
