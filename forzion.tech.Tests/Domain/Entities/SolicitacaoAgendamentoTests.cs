using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class SolicitacaoAgendamentoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly Guid LeadId = Guid.NewGuid();
    private const string SlotId = "slot-hash";
    private const string IdempotencyKey = "idem-key";
    private const string ArgumentosHash = "hash";

    private static Result<SolicitacaoAgendamento> Criar(
        Guid? treinadorId = null,
        Guid? pacoteId = null,
        Guid? leadId = null,
        string? slotId = SlotId,
        DateTime? inicioUtc = null,
        DateTime? fimUtc = null,
        string? idempotencyKey = IdempotencyKey) =>
        SolicitacaoAgendamento.Criar(
            treinadorId ?? TreinadorId,
            pacoteId ?? PacoteId,
            leadId ?? LeadId,
            slotId!,
            inicioUtc ?? TestData.Agora.AddHours(1),
            fimUtc ?? TestData.Agora.AddHours(2),
            idempotencyKey!,
            ArgumentosHash,
            TestData.Agora);

    [Fact]
    public void Criar_DadosValidos_NasceComStatusPendenteAgenteECreatedAtIgualAgora()
    {
        var inicio = TestData.Agora.AddHours(1);
        var fim = TestData.Agora.AddHours(2);

        var r = Criar(inicioUtc: inicio, fimUtc: fim);

        r.IsSuccess.Should().BeTrue();
        var s = r.Value;
        s.Id.Should().NotBeEmpty();
        s.TreinadorId.Should().Be(TreinadorId);
        s.PacoteId.Should().Be(PacoteId);
        s.LeadId.Should().Be(LeadId);
        s.SlotId.Should().Be(SlotId);
        s.InicioUtc.Should().Be(inicio);
        s.FimUtc.Should().Be(fim);
        s.IdempotencyKey.Should().Be(IdempotencyKey);
        s.ArgumentosHash.Should().Be(ArgumentosHash);
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        s.CreatedAt.Should().Be(TestData.Agora);
        s.UpdatedAt.Should().BeNull();
        s.DecididaEm.Should().BeNull();
        s.DecididaPorId.Should().BeNull();
        s.Motivo.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_Falha()
    {
        var r = Criar(treinadorId: Guid.Empty);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.treinador_id_invalido");
    }

    [Fact]
    public void Criar_PacoteIdVazio_Falha()
    {
        var r = Criar(pacoteId: Guid.Empty);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.pacote_id_invalido");
    }

    [Fact]
    public void Criar_LeadIdVazio_Falha()
    {
        var r = Criar(leadId: Guid.Empty);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.lead_id_invalido");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SlotIdVazio_Falha(string? slotId)
    {
        var r = Criar(slotId: slotId);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.slot_id_obrigatorio");
    }

    [Fact]
    public void Criar_InicioIgualFim_Falha()
    {
        var instante = TestData.Agora.AddHours(1);

        var r = Criar(inicioUtc: instante, fimUtc: instante);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.intervalo_invalido");
    }

    [Fact]
    public void Criar_InicioDepoisDoFim_Falha()
    {
        var r = Criar(inicioUtc: TestData.Agora.AddHours(2), fimUtc: TestData.Agora.AddHours(1));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.intervalo_invalido");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_IdempotencyKeyVazia_Falha(string? idempotencyKey)
    {
        var r = Criar(idempotencyKey: idempotencyKey);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.idempotency_key_obrigatoria");
    }

    [Fact]
    public void Criar_IdempotencyKeyMuitoLonga_Falha()
    {
        var r = Criar(idempotencyKey: new string('a', 201));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.idempotency_key_muito_longa");
    }

    [Fact]
    public void Criar_IdempotencyKeyNoLimiteDe200_Sucede()
    {
        var r = Criar(idempotencyKey: new string('a', 200));

        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Criar_AdicionaSolicitacaoAgendamentoCriadaEventComOsDadosDaSolicitacao()
    {
        var inicio = TestData.Agora.AddHours(1);

        var s = Criar(inicioUtc: inicio, fimUtc: inicio.AddHours(1)).Value;

        s.DomainEvents.Should().ContainSingle();
        var evento = s.DomainEvents.Single().Should().BeOfType<SolicitacaoAgendamentoCriadaEvent>().Subject;
        evento.SolicitacaoId.Should().Be(s.Id);
        evento.TreinadorId.Should().Be(TreinadorId);
        evento.PacoteId.Should().Be(PacoteId);
        evento.InicioUtc.Should().Be(inicio);
        evento.OcorridoEm.Should().Be(TestData.Agora);
    }
}
