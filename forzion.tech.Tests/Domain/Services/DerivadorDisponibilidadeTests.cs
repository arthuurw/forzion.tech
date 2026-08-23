using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Services;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Services;

public class DerivadorDisponibilidadeTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly TimeZoneInfo FusoSaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly DateTime Agora = new(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);

    // Janela ampla o bastante para cobrir os dias-alvo (segunda 2026-05-25, terça 2026-05-26)
    // independentemente do deslocamento de fuso na conversão de dia local.
    private static readonly DateTime From = new(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);

    private static ParametrosDerivacao Parametros(int duracaoMinutos, IReadOnlyList<HorarioFuncionamento> horarios) =>
        Parametros(duracaoMinutos, horarios, From, To);

    private static ParametrosDerivacao Parametros(int duracaoMinutos, IReadOnlyList<HorarioFuncionamento> horarios, DateTime from, DateTime to) =>
        new(TreinadorId, PacoteId, duracaoMinutos, from, to, Agora, FusoSaoPaulo, PoliticaAgenda.Padrao(), horarios, []);

    private static HorarioFuncionamento Horario(int diaSemana, int abreHora, int abreMinuto, int fechaHora, int fechaMinuto) =>
        HorarioFuncionamento.Criar(diaSemana, new TimeOnly(abreHora, abreMinuto), new TimeOnly(fechaHora, fechaMinuto)).Value;

    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto) =>
        new(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

    [Fact]
    public void Derivar_TurnoSimples_GeraBlocosConsecutivosComInstantesUtcCorretos()
    {
        var horario = Horario((int)DayOfWeek.Monday, 8, 0, 11, 0);
        var p = Parametros(60, [horario]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => (s.InicioUtc, s.FimUtc)).Should().Equal(
            (Utc(2026, 5, 25, 11, 0), Utc(2026, 5, 25, 12, 0)),
            (Utc(2026, 5, 25, 12, 0), Utc(2026, 5, 25, 13, 0)),
            (Utc(2026, 5, 25, 13, 0), Utc(2026, 5, 25, 14, 0)));
    }

    [Fact]
    public void Derivar_SobraParcialNaoCompletaBloco_EDescartada()
    {
        var horario = Horario((int)DayOfWeek.Monday, 8, 0, 9, 30);
        var p = Parametros(60, [horario]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().ContainSingle();
        slots[0].InicioUtc.Should().Be(Utc(2026, 5, 25, 11, 0));
        slots[0].FimUtc.Should().Be(Utc(2026, 5, 25, 12, 0));
    }

    [Fact]
    public void Derivar_DuracaoMaiorQueAJanela_NaoGeraSlotNaqueleDia()
    {
        var horario = Horario((int)DayOfWeek.Monday, 8, 0, 8, 30);
        var p = Parametros(60, [horario]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void Derivar_DoisTurnosNoMesmoDiaSemana_DerivamSeparadamenteSemFusao()
    {
        var manha = Horario((int)DayOfWeek.Monday, 8, 0, 10, 0);
        var tarde = Horario((int)DayOfWeek.Monday, 14, 0, 16, 0);
        var p = Parametros(60, [manha, tarde]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(
            Utc(2026, 5, 25, 11, 0),
            Utc(2026, 5, 25, 12, 0),
            Utc(2026, 5, 25, 17, 0),
            Utc(2026, 5, 25, 18, 0));
    }

    [Fact]
    public void Derivar_SemHorarioFuncionamento_RetornaListaVazia()
    {
        var p = Parametros(60, []);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void Derivar_TurnosSobrepostosProduzindoMesmoInicioUtc_DeduplicaPorInicioUtc()
    {
        var turno1 = Horario((int)DayOfWeek.Monday, 8, 0, 10, 0);
        var turno2 = Horario((int)DayOfWeek.Monday, 8, 0, 9, 0);
        var p = Parametros(60, [turno1, turno2]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(
            Utc(2026, 5, 25, 11, 0),
            Utc(2026, 5, 25, 12, 0));
    }

    [Fact]
    public void Derivar_HorariosEmDiasDistintosForaDeOrdem_SaiOrdenadoPorInicioUtcAscendente()
    {
        var terca = Horario((int)DayOfWeek.Tuesday, 8, 0, 9, 0);
        var segunda = Horario((int)DayOfWeek.Monday, 8, 0, 9, 0);
        var janelaEstreita = (From: Utc(2026, 5, 24, 0, 0), To: Utc(2026, 5, 27, 0, 0));
        var p = Parametros(60, [terca, segunda], janelaEstreita.From, janelaEstreita.To);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(
            Utc(2026, 5, 25, 11, 0),
            Utc(2026, 5, 26, 11, 0));
    }
}
