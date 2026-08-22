using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.ObterMetricas;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class ObterMetricasLeadsHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly ObterMetricasLeadsHandler _handler;

    public ObterMetricasLeadsHandlerTests()
    {
        _handler = new ObterMetricasLeadsHandler(_leadRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_ComLeads_CalculaTaxaDeConversaoEQuebras()
    {
        var treinadorId = Guid.NewGuid();
        var inicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var motivos = new Dictionary<MotivoDescarteLead, int> { [MotivoDescarteLead.SemInteresse] = 2 };
        _leadRepo.Setup(r => r.AgregarMetricasAsync(treinadorId, inicio, fim, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeadMetricas(10, 7, 3, 5, motivos));

        var result = await _handler.HandleAsync(new ObterMetricasLeadsQuery(treinadorId, inicio, fim));

        result.Total.Should().Be(10);
        result.PorOrigemAgente.Should().Be(7);
        result.PorOrigemManual.Should().Be(3);
        result.Convertidos.Should().Be(5);
        result.TaxaConversao.Should().Be(0.5);
        result.PorMotivoDescarte.Should().BeEquivalentTo(motivos);
    }

    [Fact]
    public async Task HandleAsync_SemLeadsNoPeriodo_RetornaZerosSemDivisaoPorZero()
    {
        var treinadorId = Guid.NewGuid();
        var inicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        _leadRepo.Setup(r => r.AgregarMetricasAsync(treinadorId, inicio, fim, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeadMetricas(0, 0, 0, 0, new Dictionary<MotivoDescarteLead, int>()));

        var result = await _handler.HandleAsync(new ObterMetricasLeadsQuery(treinadorId, inicio, fim));

        result.Total.Should().Be(0);
        result.TaxaConversao.Should().Be(0);
        result.PorMotivoDescarte.Should().BeEmpty();
    }
}
