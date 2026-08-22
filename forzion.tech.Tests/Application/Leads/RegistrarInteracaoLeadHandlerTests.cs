using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.RegistrarInteracao;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class RegistrarInteracaoLeadHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly RegistrarInteracaoLeadHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid RealizadoPorId = Guid.NewGuid();

    public RegistrarInteracaoLeadHandlerTests()
    {
        _handler = new RegistrarInteracaoLeadHandler(_leadRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static Lead NovoLead() =>
        Lead.Criar(
            TreinadorId,
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
    public async Task HandleAsync_ObservacaoValida_AnexaAoHistoricoSemMudarStatus()
    {
        var lead = NovoLead();
        SetupLead(lead);

        var result = await _handler.HandleAsync(new RegistrarInteracaoLeadCommand(TreinadorId, lead.Id, RealizadoPorId, "liguei, sem resposta"));

        result.IsSuccess.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.Novo);
        lead.Interacoes.Should().ContainSingle(i => i.Observacao == "liguei, sem resposta");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ObservacaoVazia_Recusa()
    {
        var lead = NovoLead();
        SetupLead(lead);

        var result = await _handler.HandleAsync(new RegistrarInteracaoLeadCommand(TreinadorId, lead.Id, RealizadoPorId, " "));

        result.IsFailure.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ObservacaoMuitoLonga_Recusa()
    {
        var lead = NovoLead();
        SetupLead(lead);

        var result = await _handler.HandleAsync(new RegistrarInteracaoLeadCommand(TreinadorId, lead.Id, RealizadoPorId, new string('a', 1001)));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_LeadAnonimizado_Recusa()
    {
        var lead = NovoLead();
        lead.Anonimizar(TestData.Agora);
        SetupLead(lead);

        var result = await _handler.HandleAsync(new RegistrarInteracaoLeadCommand(TreinadorId, lead.Id, RealizadoPorId, "nota"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_LeadDeOutroTreinador_RetornaNotFound()
    {
        var lead = NovoLead();
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await _handler.HandleAsync(new RegistrarInteracaoLeadCommand(TreinadorId, lead.Id, RealizadoPorId, "nota"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(forzion.tech.Domain.Shared.ErrorType.NotFound);
    }
}
