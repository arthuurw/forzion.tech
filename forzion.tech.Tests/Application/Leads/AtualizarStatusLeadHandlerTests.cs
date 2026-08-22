using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.AtualizarStatusLead;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class AtualizarStatusLeadHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly AtualizarStatusLeadHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid RealizadoPorId = Guid.NewGuid();

    public AtualizarStatusLeadHandlerTests()
    {
        _handler = new AtualizarStatusLeadHandler(_leadRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static Lead NovoLead(Guid? treinadorId = null) =>
        Lead.Criar(
            treinadorId ?? TreinadorId,
            "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", TestData.Agora, TestData.Agora).Value,
            null,
            LeadSource.Agent,
            null,
            null,
            TestData.Agora).Value;

    private void SetupLead(Lead lead) =>
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

    [Fact]
    public async Task HandleAsync_MarcarEmContato_TransicionaEGravaHistorico()
    {
        var lead = NovoLead();
        SetupLead(lead);

        var result = await _handler.HandleAsync(new AtualizarStatusLeadCommand(
            TreinadorId, lead.Id, RealizadoPorId, LeadStatus.EmContato, null, "liguei"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(LeadStatus.EmContato);
        lead.Interacoes.Should().ContainSingle(i => i.RealizadoPorId == RealizadoPorId);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DescartarSemMotivo_RecusaComValidacao()
    {
        var lead = NovoLead();
        SetupLead(lead);

        var result = await _handler.HandleAsync(new AtualizarStatusLeadCommand(
            TreinadorId, lead.Id, RealizadoPorId, LeadStatus.Descartado, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(forzion.tech.Domain.Shared.ErrorType.Validation);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DescartarComMotivo_TransicionaEGravaMotivo()
    {
        var lead = NovoLead();
        SetupLead(lead);

        var result = await _handler.HandleAsync(new AtualizarStatusLeadCommand(
            TreinadorId, lead.Id, RealizadoPorId, LeadStatus.Descartado, MotivoDescarteLead.SemInteresse, "sem retorno"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(LeadStatus.Descartado);
        lead.MotivoDescarte.Should().Be(MotivoDescarteLead.SemInteresse);
    }

    [Fact]
    public async Task HandleAsync_TransicaoAPartirDeTerminal_Recusa()
    {
        var lead = NovoLead();
        lead.Descartar(MotivoDescarteLead.SemInteresse, RealizadoPorId, null, TestData.Agora);
        SetupLead(lead);

        var result = await _handler.HandleAsync(new AtualizarStatusLeadCommand(
            TreinadorId, lead.Id, RealizadoPorId, LeadStatus.EmContato, null, null));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_LeadAnonimizado_Recusa()
    {
        var lead = NovoLead();
        lead.Anonimizar(TestData.Agora);
        SetupLead(lead);

        var result = await _handler.HandleAsync(new AtualizarStatusLeadCommand(
            TreinadorId, lead.Id, RealizadoPorId, LeadStatus.EmContato, null, null));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_LeadDeOutroTreinador_RetornaNotFound()
    {
        var lead = NovoLead();
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await _handler.HandleAsync(new AtualizarStatusLeadCommand(
            TreinadorId, lead.Id, RealizadoPorId, LeadStatus.EmContato, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(forzion.tech.Domain.Shared.ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_TentaConverterPorEstaRota_Recusa()
    {
        var lead = NovoLead();
        SetupLead(lead);

        var result = await _handler.HandleAsync(new AtualizarStatusLeadCommand(
            TreinadorId, lead.Id, RealizadoPorId, LeadStatus.Convertido, null, null));

        result.IsFailure.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.Novo);
    }
}
