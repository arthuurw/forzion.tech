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
        Parametros(duracaoMinutos, horarios, From, To, []);

    private static ParametrosDerivacao Parametros(int duracaoMinutos, IReadOnlyList<HorarioFuncionamento> horarios, DateTime from, DateTime to) =>
        Parametros(duracaoMinutos, horarios, from, to, []);

    private static ParametrosDerivacao Parametros(int duracaoMinutos, IReadOnlyList<HorarioFuncionamento> horarios, IReadOnlyList<BloqueioAgenda> bloqueios) =>
        Parametros(duracaoMinutos, horarios, From, To, bloqueios);

    private static ParametrosDerivacao Parametros(int duracaoMinutos, IReadOnlyList<HorarioFuncionamento> horarios, DateTime from, DateTime to, IReadOnlyList<BloqueioAgenda> bloqueios) =>
        new(TreinadorId, PacoteId, duracaoMinutos, from, to, Agora, FusoSaoPaulo, PoliticaAgenda.Padrao(), horarios, bloqueios);

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

    // --- Aplicação de bloqueios ---
    // Turno segunda 08:00-11:00 (local, America/Sao_Paulo, UTC-3) => slots 08,09,10 local = 11,12,13 UTC.

    private static ParametrosDerivacao ParametrosComTurnoENoBloqueios(IReadOnlyList<BloqueioAgenda> bloqueios)
    {
        var horario = Horario((int)DayOfWeek.Monday, 8, 0, 11, 0);
        return Parametros(60, [horario], bloqueios);
    }

    [Fact]
    public void Derivar_BloqueioPontualCobreUmSlot_OmiteApenasEsseSlot()
    {
        var bloqueio = BloqueioAgenda.CriarPontual(TreinadorId, Utc(2026, 5, 25, 12, 0), Utc(2026, 5, 25, 13, 0), null, Agora).Value;
        var p = ParametrosComTurnoENoBloqueios([bloqueio]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(
            Utc(2026, 5, 25, 11, 0),
            Utc(2026, 5, 25, 13, 0));
    }

    [Fact]
    public void Derivar_BloqueioRecorrenteCobreUmSlot_ComparadoNoFusoDoTreinador_OmiteApenasEsseSlot()
    {
        var bloqueio = BloqueioAgenda.CriarRecorrente(TreinadorId, (int)DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), null, Agora).Value;
        var p = ParametrosComTurnoENoBloqueios([bloqueio]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(
            Utc(2026, 5, 25, 11, 0),
            Utc(2026, 5, 25, 13, 0));
    }

    [Fact]
    public void Derivar_BloqueioCobreSlotApenasParcialmente_OmiteOSlotInteiro()
    {
        var bloqueio = BloqueioAgenda.CriarPontual(TreinadorId, Utc(2026, 5, 25, 12, 30), Utc(2026, 5, 25, 12, 45), null, Agora).Value;
        var p = ParametrosComTurnoENoBloqueios([bloqueio]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(
            Utc(2026, 5, 25, 11, 0),
            Utc(2026, 5, 25, 13, 0));
    }

    [Fact]
    public void Derivar_BloqueioQueNaoIntersectaNenhumSlot_NaoRemoveNada()
    {
        var bloqueio = BloqueioAgenda.CriarPontual(TreinadorId, Utc(2026, 5, 25, 23, 0), Utc(2026, 5, 26, 0, 0), null, Agora).Value;
        var p = ParametrosComTurnoENoBloqueios([bloqueio]);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(
            Utc(2026, 5, 25, 11, 0),
            Utc(2026, 5, 25, 12, 0),
            Utc(2026, 5, 25, 13, 0));
    }

    // --- Antecedência mínima e horizonte ---
    // Segunda 2026-05-25, dois turnos de 1h sem slot intermediário: 08:00-09:00 e 10:00-11:00 local
    // (America/Sao_Paulo, UTC-3) = 11:00-12:00 e 13:00-14:00 UTC.

    private static readonly HorarioFuncionamento TurnoManhaCedo = Horario((int)DayOfWeek.Monday, 8, 0, 9, 0);
    private static readonly HorarioFuncionamento TurnoManhaTarde = Horario((int)DayOfWeek.Monday, 10, 0, 11, 0);

    private static ParametrosDerivacao ParametrosComPolitica(DateTime agora, DateTime from, DateTime to, PoliticaAgenda politica) =>
        new(TreinadorId, PacoteId, 60, from, to, agora, FusoSaoPaulo, politica, [TurnoManhaCedo, TurnoManhaTarde], []);

    [Fact]
    public void Derivar_AntecedenciaMinima_RemoveSlotAntesDoCorteEMantemOSeguinte()
    {
        var agora = Utc(2026, 5, 25, 10, 30); // 07:30 local
        var politica = PoliticaAgenda.Criar(2, 60).Value; // corte: agora + 2h = 12:30 UTC (09:30 local)
        var p = ParametrosComPolitica(agora, From, To, politica);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(Utc(2026, 5, 25, 13, 0));
    }

    [Fact]
    public void Derivar_Horizonte_CortaFimMesmoQuandoToAlcancaMaisLonge()
    {
        var agora = Utc(2026, 5, 20, 0, 0);
        var politica = PoliticaAgenda.Criar(0, 1).Value; // horizonte de 1 dia: agora + 1d = 2026-05-21, antes da segunda 25/05
        var p = ParametrosComPolitica(agora, From, To, politica); // To = 2026-05-28, bem além do horizonte

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void Derivar_InicioEfetivoMaiorOuIgualFimEfetivo_RetornaListaVazia()
    {
        var agora = Utc(2026, 5, 25, 10, 30);
        var politica = PoliticaAgenda.Criar(62, 60).Value; // corte: agora + 62h ultrapassa To (2026-05-28T00:00Z)
        var p = ParametrosComPolitica(agora, From, To, politica);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void Derivar_AntecedenciaContaAPartirDeAgora_MesmoComFromNoPassado()
    {
        var agora = Utc(2026, 5, 25, 10, 30); // 07:30 local
        var fromNoPassado = Utc(2026, 1, 1, 0, 0);
        var politica = PoliticaAgenda.Criar(2, 60).Value;
        var p = ParametrosComPolitica(agora, fromNoPassado, To, politica);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Select(s => s.InicioUtc).Should().Equal(Utc(2026, 5, 25, 13, 0));
    }

    // --- LocalizarPorId ---

    [Fact]
    public void LocalizarPorId_IdDeSlotValido_DevolveOSlotComInicioEFimCorretos()
    {
        var p = ParametrosComTurnoENoBloqueios([]);
        var slotId = SlotId.Calcular(TreinadorId, PacoteId, Utc(2026, 5, 25, 12, 0));

        var slot = DerivadorDisponibilidade.LocalizarPorId(p, slotId);

        slot.Should().NotBeNull();
        slot!.InicioUtc.Should().Be(Utc(2026, 5, 25, 12, 0));
        slot.FimUtc.Should().Be(Utc(2026, 5, 25, 13, 0));
    }

    [Fact]
    public void LocalizarPorId_IdCobertoPorBloqueio_DevolveNull()
    {
        var bloqueio = BloqueioAgenda.CriarPontual(TreinadorId, Utc(2026, 5, 25, 12, 0), Utc(2026, 5, 25, 13, 0), null, Agora).Value;
        var p = ParametrosComTurnoENoBloqueios([bloqueio]);
        var slotId = SlotId.Calcular(TreinadorId, PacoteId, Utc(2026, 5, 25, 12, 0));

        var slot = DerivadorDisponibilidade.LocalizarPorId(p, slotId);

        slot.Should().BeNull();
    }

    [Fact]
    public void LocalizarPorId_IdAlemDoHorizonteDias_DevolveNull()
    {
        var agora = Utc(2026, 5, 20, 0, 0);
        var politica = PoliticaAgenda.Criar(0, 1).Value; // horizonte de 1 dia: exclui a segunda 25/05
        var p = ParametrosComPolitica(agora, From, To, politica);
        var slotId = SlotId.Calcular(TreinadorId, PacoteId, Utc(2026, 5, 25, 11, 0));

        var slot = DerivadorDisponibilidade.LocalizarPorId(p, slotId);

        slot.Should().BeNull();
    }

    [Fact]
    public void LocalizarPorId_IdArbitrario_DevolveNull()
    {
        var horario = Horario((int)DayOfWeek.Monday, 8, 0, 11, 0);
        var p = Parametros(60, [horario]);

        var slot = DerivadorDisponibilidade.LocalizarPorId(p, "id-que-nunca-existiu");

        slot.Should().BeNull();
    }

    // --- FimUtc derivado do início, não de segunda conversão de fuso ---
    // Fim do horário de verão em America/New_York em 2026-11-01 02:00 local (EDT UTC-4 -> EST UTC-5).

    private static readonly TimeZoneInfo FusoNewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    [Fact]
    public void Derivar_SlotAtravessaFimDoHorarioDeVeraoEmNewYork_FimUtcEInicioUtcMaisDuracaoNaoConversaoIndependente()
    {
        var horario = Horario((int)DayOfWeek.Sunday, 0, 30, 2, 0);
        var agora = Utc(2026, 10, 1, 0, 0);
        var from = Utc(2026, 10, 26, 12, 0);
        var to = Utc(2026, 11, 5, 0, 0);
        var p = new ParametrosDerivacao(TreinadorId, PacoteId, 90, from, to, agora, FusoNewYork, PoliticaAgenda.Padrao(), [horario], []);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().ContainSingle();
        slots[0].InicioUtc.Should().Be(Utc(2026, 11, 1, 4, 30));
        slots[0].FimUtc.Should().Be(Utc(2026, 11, 1, 6, 0));
    }

    [Fact]
    public void Derivar_VariosSlotsNaVirasDeFimDoHorarioDeVeraoEmNewYork_TodosRespeitamFimUtcIgualInicioUtcMaisDuracao()
    {
        var horario = Horario((int)DayOfWeek.Sunday, 0, 0, 4, 0);
        var agora = Utc(2026, 10, 1, 0, 0);
        var from = Utc(2026, 10, 26, 12, 0);
        var to = Utc(2026, 11, 5, 0, 0);
        var p = new ParametrosDerivacao(TreinadorId, PacoteId, 90, from, to, agora, FusoNewYork, PoliticaAgenda.Padrao(), [horario], []);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().HaveCount(2);
        slots.Should().OnlyContain(s => s.FimUtc == s.InicioUtc.AddMinutes(90));
    }

    // --- Cap de slots materializados ---
    // America/Sao_Paulo não observa DST desde 2019: janelas de dia local ficam exatas em UTC-3.

    private static readonly TimeZoneInfo FusoSaoPauloSemDst = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly IReadOnlyList<HorarioFuncionamento> HorariosDiaInteiroTodosOsDias =
        Enumerable.Range(0, 7).Select(dia => Horario(dia, 0, 0, 23, 45)).ToList();
    private const int BlocosPorDiaInteiro = 95; // (23h45 - 0h00) / 15min

    [Fact]
    public void Derivar_HorizonteMaximoComJanelasLargasEDuracaoCurta_ParaExatamenteNoCapSemEstourarAContagem()
    {
        var agora = Utc(2026, 6, 1, 3, 0); // 2026-06-01 00:00 local (America/Sao_Paulo, UTC-3)
        var politica = PoliticaAgenda.Criar(0, 365).Value;
        var p = new ParametrosDerivacao(TreinadorId, PacoteId, 15, agora, agora.AddDays(400), agora, FusoSaoPauloSemDst, politica, HorariosDiaInteiroTodosOsDias, []);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().HaveCount(ParametrosDerivacao.MaxSlotsMaterializados);
    }

    [Fact]
    public void Derivar_TotalDeSlotsAbaixoDoCap_RetornaTodosSemTruncar()
    {
        const int diasDeHorizonte = 100;
        var agora = Utc(2026, 6, 1, 3, 0); // 2026-06-01 00:00 local (America/Sao_Paulo, UTC-3)
        var politica = PoliticaAgenda.Criar(0, diasDeHorizonte).Value;
        var p = new ParametrosDerivacao(TreinadorId, PacoteId, 15, agora, agora.AddDays(150), agora, FusoSaoPauloSemDst, politica, HorariosDiaInteiroTodosOsDias, []);

        var slots = DerivadorDisponibilidade.Derivar(p);

        slots.Should().HaveCount(diasDeHorizonte * BlocosPorDiaInteiro);
    }
}
