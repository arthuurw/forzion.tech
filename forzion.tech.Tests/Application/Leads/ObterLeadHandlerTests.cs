using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.ObterLead;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class ObterLeadHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly ObterLeadHandler _handler;

    public ObterLeadHandlerTests()
    {
        _handler = new ObterLeadHandler(_leadRepo.Object);
    }

    private static Lead NovoLead(Guid treinadorId) =>
        Lead.Criar(
            treinadorId,
            "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", TestData.Agora, TestData.Agora).Value,
            null,
            LeadSource.Agent,
            null,
            null,
            TestData.Agora).Value;

    [Fact]
    public async Task HandleAsync_LeadExiste_RetornaDetalheComHistoricoCronologico()
    {
        var treinadorId = Guid.NewGuid();
        var lead = NovoLead(treinadorId);
        lead.RegistrarInteracao(Guid.NewGuid(), "segunda nota", TestData.Agora.AddHours(2));
        lead.MarcarEmContato(Guid.NewGuid(), "primeira nota", TestData.Agora.AddHours(1));
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(treinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var result = await _handler.HandleAsync(new ObterLeadQuery(treinadorId, lead.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.ContatoValor.Should().Be("fulano@lead.com");
        result.Value.Historico.Should().HaveCount(2);
        result.Value.Historico[0].OcorridaEm.Should().BeBefore(result.Value.Historico[1].OcorridaEm);
    }

    [Fact]
    public async Task HandleAsync_LeadNaoEncontrado_RetornaNotFound()
    {
        var treinadorId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(treinadorId, leadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await _handler.HandleAsync(new ObterLeadQuery(treinadorId, leadId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_LeadDeOutroTreinador_RetornaMesmoNotFoundDeInexistente()
    {
        var treinadorA = Guid.NewGuid();
        var treinadorB = Guid.NewGuid();
        var lead = NovoLead(treinadorB);
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(treinadorA, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await _handler.HandleAsync(new ObterLeadQuery(treinadorA, lead.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(forzion.tech.Domain.Shared.Errors.LeadErrors.NaoEncontrado);
    }
}
