using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class LeadEstadoTests
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid RealizadoPorId = Guid.NewGuid();

    private static Lead NovoLead(DateTime? criadoEm = null)
    {
        var contato = ContatoLead.Criar(TipoContatoLead.Email, "lead@example.com").Value;
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;
        return Lead.Criar(TreinadorId, "Fulano", contato, null, consentimento, null, LeadSource.Agent, null, null, criadoEm ?? Agora).Value;
    }

    [Fact]
    public void MarcarEmContato_APartirDeNovo_TransicionaERegistraHistorico()
    {
        var lead = NovoLead();
        var depois = Agora.AddHours(1);

        var r = lead.MarcarEmContato(RealizadoPorId, "liguei", depois);

        r.IsSuccess.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.EmContato);
        lead.UltimoToqueEm.Should().Be(depois);
        lead.UpdatedAt.Should().Be(depois);
        lead.Interacoes.Should().ContainSingle();
        var interacao = lead.Interacoes[0];
        interacao.StatusAnterior.Should().Be(LeadStatus.Novo);
        interacao.StatusNovo.Should().Be(LeadStatus.EmContato);
        interacao.Observacao.Should().Be("liguei");
        interacao.RealizadoPorId.Should().Be(RealizadoPorId);
        interacao.OcorridaEm.Should().Be(depois);
    }

    [Fact]
    public void MarcarEmContato_APartirDeEmContato_Falha()
    {
        var lead = NovoLead();
        lead.MarcarEmContato(RealizadoPorId, null, Agora);

        var r = lead.MarcarEmContato(RealizadoPorId, null, Agora.AddHours(1));

        r.IsFailure.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.EmContato);
    }

    [Fact]
    public void MarcarEmContato_APartirDeConvertido_Falha()
    {
        var lead = NovoLead();
        lead.Converter(Guid.NewGuid(), Agora);

        var r = lead.MarcarEmContato(RealizadoPorId, null, Agora.AddHours(1));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarEmContato_APartirDeDescartado_Falha()
    {
        var lead = NovoLead();
        lead.Descartar(MotivoDescarteLead.SemInteresse, RealizadoPorId, null, Agora);

        var r = lead.MarcarEmContato(RealizadoPorId, null, Agora.AddHours(1));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarInteracao_NaoAlteraStatusEAtualizaUltimoToque()
    {
        var lead = NovoLead();
        var depois = Agora.AddHours(2);

        var r = lead.RegistrarInteracao(RealizadoPorId, "conversamos por whatsapp", depois);

        r.IsSuccess.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.Novo);
        lead.UltimoToqueEm.Should().Be(depois);
        lead.Interacoes.Should().ContainSingle();
        lead.Interacoes[0].StatusNovo.Should().BeNull();
        lead.Interacoes[0].Observacao.Should().Be("conversamos por whatsapp");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegistrarInteracao_ObservacaoAusente_Falha(string observacao)
    {
        var lead = NovoLead();

        var r = lead.RegistrarInteracao(RealizadoPorId, observacao, Agora);

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarInteracao_ObservacaoAcimaDe1000_Falha()
    {
        var lead = NovoLead();
        var observacao = new string('a', 1001);

        var r = lead.RegistrarInteracao(RealizadoPorId, observacao, Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("1000");
    }

    [Fact]
    public void RegistrarInteracao_APartirDeEstadoTerminal_Falha()
    {
        var lead = NovoLead();
        lead.Converter(Guid.NewGuid(), Agora);

        var r = lead.RegistrarInteracao(RealizadoPorId, "nota", Agora.AddHours(1));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Converter_ComAlunoIdValido_TransicionaEGravaAlunoId()
    {
        var lead = NovoLead();
        var alunoId = Guid.NewGuid();
        var depois = Agora.AddDays(1);

        var r = lead.Converter(alunoId, depois);

        r.IsSuccess.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.Convertido);
        lead.AlunoId.Should().Be(alunoId);
        lead.Interacoes.Should().ContainSingle();
        lead.Interacoes[0].StatusNovo.Should().Be(LeadStatus.Convertido);
    }

    [Fact]
    public void Converter_AlunoIdVazio_Falha()
    {
        var lead = NovoLead();

        var r = lead.Converter(Guid.Empty, Agora);

        r.IsFailure.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.Novo);
    }

    [Fact]
    public void Converter_APartirDeEstadoTerminal_Falha()
    {
        var lead = NovoLead();
        lead.Descartar(MotivoDescarteLead.SemInteresse, RealizadoPorId, null, Agora);

        var r = lead.Converter(Guid.NewGuid(), Agora.AddHours(1));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Descartar_ComMotivoValido_TransicionaEGravaMotivoEObservacao()
    {
        var lead = NovoLead();
        var depois = Agora.AddHours(3);

        var r = lead.Descartar(MotivoDescarteLead.Duplicado, RealizadoPorId, "ja e aluno", depois);

        r.IsSuccess.Should().BeTrue();
        lead.Status.Should().Be(LeadStatus.Descartado);
        lead.MotivoDescarte.Should().Be(MotivoDescarteLead.Duplicado);
        lead.Interacoes.Should().ContainSingle();
        lead.Interacoes[0].Observacao.Should().Be("ja e aluno");
    }

    [Fact]
    public void Descartar_APartirDeEstadoTerminal_Falha()
    {
        var lead = NovoLead();
        lead.Converter(Guid.NewGuid(), Agora);

        var r = lead.Descartar(MotivoDescarteLead.Outro, RealizadoPorId, null, Agora.AddHours(1));

        r.IsFailure.Should().BeTrue();
    }
}
