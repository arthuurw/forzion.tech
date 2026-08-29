using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class LeadAnonimizarTests
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid RealizadoPorId = Guid.NewGuid();

    private static Lead LeadComHistorico()
    {
        var contato = ContatoLead.Criar(TipoContatoLead.Email, "lead@example.com").Value;
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;
        var origem = OrigemLead.Criar("Mozilla/5.0", "gateway-gpt").Value;
        var lead = Lead.Criar(TreinadorId, "Fulano", contato, "quero treinar", consentimento, origem, LeadSource.Agent, "chave", "hash", Agora).Value;
        lead.MarcarEmContato(RealizadoPorId, "liguei ontem", Agora.AddHours(1));
        return lead;
    }

    [Fact]
    public void Anonimizar_EscruaNomeContatoInteresseEFinalidade()
    {
        var lead = LeadComHistorico();

        var r = lead.Anonimizar(Agora.AddDays(180));

        r.IsSuccess.Should().BeTrue();
        lead.Anonimizado.Should().BeTrue();
        lead.Nome.Should().Be("Lead anonimizado");
        lead.Contato.Valor.Should().Be("[anonimizado]");
        lead.Interesse.Should().BeNull();
        lead.Consentimento.Finalidade.Should().Be("[anonimizado]");
    }

    [Fact]
    public void Anonimizar_ZeraArgumentosHashEIdempotencyKey()
    {
        var lead = LeadComHistorico();

        lead.Anonimizar(Agora.AddDays(180));

        lead.ArgumentosHash.Should().BeNull();
        lead.IdempotencyKey.Should().BeNull();
    }

    [Fact]
    public void Anonimizar_EscruaObservacoesDoHistorico()
    {
        var lead = LeadComHistorico();

        lead.Anonimizar(Agora.AddDays(180));

        lead.Interacoes.Should().ContainSingle().Which.Observacao.Should().Be("[anonimizado]");
    }

    [Fact]
    public void Anonimizar_PreservaSourceOrigemStatusMotivoDescarteAlunoIdEDatas()
    {
        var lead = LeadComHistorico();
        var source = lead.Source;
        var status = lead.Status;
        var origem = lead.Origem;
        var createdAt = lead.CreatedAt;

        lead.Anonimizar(Agora.AddDays(180));

        lead.Source.Should().Be(source);
        lead.Status.Should().Be(status);
        lead.MotivoDescarte.Should().BeNull();
        lead.AlunoId.Should().BeNull();
        lead.Origem.Should().BeEquivalentTo(origem);
        lead.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Anonimizar_PreservaAlunoIdQuandoConvertido()
    {
        var lead = LeadComHistorico();
        var alunoId = Guid.NewGuid();
        lead.Converter(alunoId, Agora.AddHours(2));

        lead.Anonimizar(Agora.AddDays(180));

        lead.AlunoId.Should().Be(alunoId);
        lead.Status.Should().Be(LeadStatus.Convertido);
    }

    [Fact]
    public void Anonimizar_ChamadoDuasVezes_EIdempotenteSemErro()
    {
        var lead = LeadComHistorico();
        lead.Anonimizar(Agora.AddDays(180));
        var nomeAposPrimeiraChamada = lead.Nome;
        var updatedAtAposPrimeiraChamada = lead.UpdatedAt;

        var r = lead.Anonimizar(Agora.AddDays(181));

        r.IsSuccess.Should().BeTrue();
        lead.Nome.Should().Be(nomeAposPrimeiraChamada);
        lead.UpdatedAt.Should().Be(updatedAtAposPrimeiraChamada);
    }

    [Fact]
    public void MarcarEmContato_EmLeadAnonimizado_Falha()
    {
        var lead = LeadComHistorico();
        lead.Anonimizar(Agora.AddDays(180));

        var r = lead.MarcarEmContato(RealizadoPorId, null, Agora.AddDays(181));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarInteracao_EmLeadAnonimizado_Falha()
    {
        var lead = LeadComHistorico();
        lead.Anonimizar(Agora.AddDays(180));

        var r = lead.RegistrarInteracao(RealizadoPorId, "nota", Agora.AddDays(181));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Converter_EmLeadAnonimizado_Falha()
    {
        var lead = LeadComHistorico();
        lead.Anonimizar(Agora.AddDays(180));

        var r = lead.Converter(Guid.NewGuid(), Agora.AddDays(181));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Descartar_EmLeadAnonimizado_Falha()
    {
        var lead = LeadComHistorico();
        lead.Anonimizar(Agora.AddDays(180));

        var r = lead.Descartar(MotivoDescarteLead.Outro, RealizadoPorId, null, Agora.AddDays(181));

        r.IsFailure.Should().BeTrue();
    }
}
