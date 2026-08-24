using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class SolicitacaoAgendamentoMaquinaDeEstadosTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly Guid LeadId = Guid.NewGuid();

    private static SolicitacaoAgendamento CriarPendente(DateTime? inicioUtc = null) =>
        SolicitacaoAgendamento.Criar(
            TreinadorId, PacoteId, LeadId, "slot-hash",
            inicioUtc ?? TestData.Agora.AddHours(1), (inicioUtc ?? TestData.Agora.AddHours(1)).AddHours(1),
            "idem-key", "hash", TestData.Agora).Value;

    private static SolicitacaoAgendamento CriarConfirmada()
    {
        var s = CriarPendente();
        s.Confirmar(Guid.NewGuid(), TestData.Agora).IsSuccess.Should().BeTrue();
        return s;
    }

    private static SolicitacaoAgendamento CriarRecusada()
    {
        var s = CriarPendente();
        s.Recusar(Guid.NewGuid(), null, TestData.Agora).IsSuccess.Should().BeTrue();
        return s;
    }

    private static SolicitacaoAgendamento CriarCancelada()
    {
        var s = CriarConfirmada();
        s.Cancelar(Guid.NewGuid(), null, TestData.Agora).IsSuccess.Should().BeTrue();
        return s;
    }

    // --- Confirmar: happy path ---

    [Fact]
    public void Confirmar_PendenteComInicioFuturo_MudaParaConfirmadaEGravaDecisao()
    {
        var s = CriarPendente(TestData.Agora.AddHours(1));
        var realizadoPorId = Guid.NewGuid();

        var r = s.Confirmar(realizadoPorId, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Confirmada);
        s.DecididaEm.Should().Be(TestData.Agora);
        s.DecididaPorId.Should().Be(realizadoPorId);
        s.UpdatedAt.Should().Be(TestData.Agora);
    }

    [Fact]
    public void Confirmar_SlotJaIniciado_FalhaSemMutar()
    {
        var s = CriarPendente(TestData.Agora);

        var r = s.Confirmar(Guid.NewGuid(), TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.slot_ja_iniciado");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        s.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Confirmar_SlotNoPassado_FalhaSemMutar()
    {
        var s = CriarPendente(TestData.Agora.AddHours(-2));

        var r = s.Confirmar(Guid.NewGuid(), TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.slot_ja_iniciado");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
    }

    // --- Confirmar: transições inválidas ---

    [Fact]
    public void Confirmar_DeConfirmada_FalhaSemMutar()
    {
        var s = CriarConfirmada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Confirmar(Guid.NewGuid(), TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Confirmada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    [Fact]
    public void Confirmar_DeRecusada_FalhaSemMutar()
    {
        var s = CriarRecusada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Confirmar(Guid.NewGuid(), TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Recusada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    [Fact]
    public void Confirmar_DeCancelada_FalhaSemMutar()
    {
        var s = CriarCancelada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Confirmar(Guid.NewGuid(), TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Cancelada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    // --- Recusar: happy path ---

    [Fact]
    public void Recusar_PendenteSemMotivo_MudaParaRecusadaComMotivoNulo()
    {
        var s = CriarPendente();
        var realizadoPorId = Guid.NewGuid();

        var r = s.Recusar(realizadoPorId, null, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Recusada);
        s.Motivo.Should().BeNull();
        s.DecididaEm.Should().Be(TestData.Agora);
        s.DecididaPorId.Should().Be(realizadoPorId);
    }

    [Fact]
    public void Recusar_PendenteComMotivo_NormalizaERegistraOMotivo()
    {
        var s = CriarPendente();

        var r = s.Recusar(Guid.NewGuid(), "  Agenda cheia  ", TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        s.Motivo.Should().Be("Agenda cheia");
    }

    [Fact]
    public void Recusar_MotivoSoWhitespace_MotivoNulo()
    {
        var s = CriarPendente();

        var r = s.Recusar(Guid.NewGuid(), "   ", TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        s.Motivo.Should().BeNull();
    }

    [Fact]
    public void Recusar_MotivoMuitoLongo_FalhaSemMutar()
    {
        var s = CriarPendente();

        var r = s.Recusar(Guid.NewGuid(), new string('a', 501), TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.motivo_muito_longo");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        s.Motivo.Should().BeNull();
        s.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Recusar_MotivoNoLimiteDe500_Sucede()
    {
        var s = CriarPendente();

        var r = s.Recusar(Guid.NewGuid(), new string('a', 500), TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        s.Motivo!.Length.Should().Be(500);
    }

    // --- Recusar: transições inválidas ---

    [Fact]
    public void Recusar_DeConfirmada_FalhaSemMutar()
    {
        var s = CriarConfirmada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Recusar(Guid.NewGuid(), "motivo", TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Confirmada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    [Fact]
    public void Recusar_DeRecusada_FalhaSemMutar()
    {
        var s = CriarRecusada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Recusar(Guid.NewGuid(), "motivo", TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Recusada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    [Fact]
    public void Recusar_DeCancelada_FalhaSemMutar()
    {
        var s = CriarCancelada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Recusar(Guid.NewGuid(), "motivo", TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Cancelada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    // --- Cancelar: happy path ---

    [Fact]
    public void Cancelar_Confirmada_MudaParaCanceladaEGravaDecisao()
    {
        var s = CriarConfirmada();
        var realizadoPorId = Guid.NewGuid();
        var agoraCancelamento = TestData.Agora.AddMinutes(10);

        var r = s.Cancelar(realizadoPorId, "não vai dar", agoraCancelamento);

        r.IsSuccess.Should().BeTrue();
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Cancelada);
        s.Motivo.Should().Be("não vai dar");
        s.DecididaEm.Should().Be(agoraCancelamento);
        s.DecididaPorId.Should().Be(realizadoPorId);
        s.UpdatedAt.Should().Be(agoraCancelamento);
    }

    [Fact]
    public void Cancelar_MotivoMuitoLongo_FalhaSemMutar()
    {
        var s = CriarConfirmada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Cancelar(Guid.NewGuid(), new string('a', 501), TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.motivo_muito_longo");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Confirmada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    // --- Cancelar: transições inválidas ---

    [Fact]
    public void Cancelar_DePendenteAgente_FalhaSemMutar()
    {
        var s = CriarPendente();

        var r = s.Cancelar(Guid.NewGuid(), null, TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        s.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Cancelar_DeRecusada_FalhaSemMutar()
    {
        var s = CriarRecusada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Cancelar(Guid.NewGuid(), null, TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Recusada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }

    [Fact]
    public void Cancelar_DeCancelada_FalhaSemMutar()
    {
        var s = CriarCancelada();
        var updatedAtAntes = s.UpdatedAt;

        var r = s.Cancelar(Guid.NewGuid(), null, TestData.Agora.AddMinutes(5));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("solicitacao_agendamento.transicao_nao_suportada");
        s.Status.Should().Be(SolicitacaoAgendamentoStatus.Cancelada);
        s.UpdatedAt.Should().Be(updatedAtAntes);
    }
}
