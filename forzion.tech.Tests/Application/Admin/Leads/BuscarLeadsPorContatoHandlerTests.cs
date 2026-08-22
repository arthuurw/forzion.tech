using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.Leads;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Moq;

namespace forzion.tech.Tests.Application.Admin.Leads;

public class BuscarLeadsPorContatoHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly BuscarLeadsPorContatoHandler _handler;

    public BuscarLeadsPorContatoHandlerTests()
    {
        _handler = new BuscarLeadsPorContatoHandler(_leadRepo.Object);
    }

    private static Lead NovoLead(Guid treinadorId, TipoContatoLead tipo, string valor) =>
        Lead.Criar(
            treinadorId, "Fulano",
            ContatoLead.Criar(tipo, valor).Value,
            null,
            ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, null, null, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_BuscaPorEmail_NormalizaAntesDeBuscar()
    {
        var lead = NovoLead(Guid.NewGuid(), TipoContatoLead.Email, "fulano@lead.com");
        _leadRepo.Setup(r => r.BuscarPorContatoCrossTenantAsync("fulano@lead.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead]);

        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("  Fulano@Lead.com  "));

        result.Should().ContainSingle(i => i.Id == lead.Id);
        _leadRepo.Verify(r => r.BuscarPorContatoCrossTenantAsync("fulano@lead.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BuscaPorTelefoneEmFormatoDiferente_CasaComORegistroNormalizado()
    {
        var lead = NovoLead(Guid.NewGuid(), TipoContatoLead.Telefone, "5511999998888");
        _leadRepo.Setup(r => r.BuscarPorContatoCrossTenantAsync("5511999998888", It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead]);

        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("(11) 99999-8888"));

        result.Should().ContainSingle(i => i.Id == lead.Id);
    }

    [Fact]
    public async Task HandleAsync_SemResultado_RetornaListaVazia()
    {
        _leadRepo.Setup(r => r.BuscarPorContatoCrossTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("naoexiste@lead.com"));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ContatoMalFormado_RetornaListaVaziaSemErro()
    {
        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("abc"));

        result.Should().BeEmpty();
        _leadRepo.Verify(r => r.BuscarPorContatoCrossTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
