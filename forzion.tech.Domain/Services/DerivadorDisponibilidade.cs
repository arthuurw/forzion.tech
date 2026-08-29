using forzion.tech.Domain.Entities;

namespace forzion.tech.Domain.Services;

public static class DerivadorDisponibilidade
{
    // Busca pontual: percorre os candidatos preguiçosamente e para no primeiro slotId igual, sem
    // materializar/ordenar/deduplicar o conjunto inteiro como Derivar faz para a listagem.
    public static SlotDisponivel? LocalizarPorId(ParametrosDerivacao p, string slotId)
    {
        var (inicioEfetivo, fimEfetivo) = JanelaEfetiva(p);
        if (inicioEfetivo >= fimEfetivo)
            return null;

        return CandidatosNaJanela(p, inicioEfetivo, fimEfetivo).FirstOrDefault(s => s.SlotId == slotId);
    }

    public static IReadOnlyList<SlotDisponivel> Derivar(ParametrosDerivacao p)
    {
        var (inicioEfetivo, fimEfetivo) = JanelaEfetiva(p);
        if (inicioEfetivo >= fimEfetivo)
            return [];

        var candidatos = new List<SlotDisponivel>();
        foreach (var slot in CandidatosNaJanela(p, inicioEfetivo, fimEfetivo))
        {
            candidatos.Add(slot);
            if (candidatos.Count >= ParametrosDerivacao.MaxSlotsMaterializados)
                break;
        }

        return candidatos
            .DistinctBy(s => s.InicioUtc)
            .OrderBy(s => s.InicioUtc)
            .Take(ParametrosDerivacao.MaxSlotsMaterializados)
            .ToList();
    }

    private static (DateTime InicioEfetivo, DateTime FimEfetivo) JanelaEfetiva(ParametrosDerivacao p) => (
        Maior(p.From, p.Agora.AddHours(p.Politica.AntecedenciaMinimaHoras)),
        Menor(p.To, p.Agora.AddDays(p.Politica.HorizonteDias)));

    private static IEnumerable<SlotDisponivel> CandidatosNaJanela(ParametrosDerivacao p, DateTime inicioEfetivo, DateTime fimEfetivo)
    {
        foreach (var diaLocal in DiasNoIntervalo(inicioEfetivo, fimEfetivo, p.Fuso))
        {
            var diaSemana = (int)diaLocal.DayOfWeek;
            var horariosDoDia = p.Horarios.Where(h => h.DiaSemana == diaSemana);

            foreach (var horario in horariosDoDia)
                foreach (var slot in BlocosDoTurno(p, diaLocal, horario))
                {
                    if (slot.InicioUtc >= inicioEfetivo && slot.InicioUtc < fimEfetivo
                        && !p.Bloqueios.Any(b => b.Cobre(slot.InicioUtc, slot.FimUtc, p.Fuso)))
                        yield return slot;
                }
        }
    }

    private static DateTime Maior(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Menor(DateTime a, DateTime b) => a < b ? a : b;

    private static IEnumerable<SlotDisponivel> BlocosDoTurno(ParametrosDerivacao p, DateTime diaLocal, HorarioFuncionamento horario)
    {
        var abreMinutos = horario.AbreAs.Hour * 60 + horario.AbreAs.Minute;
        var fechaMinutos = horario.FechaAs.Hour * 60 + horario.FechaAs.Minute;

        for (var inicioMinutos = abreMinutos; inicioMinutos + p.DuracaoMinutos <= fechaMinutos; inicioMinutos += p.DuracaoMinutos)
        {
            var inicioLocal = diaLocal.AddMinutes(inicioMinutos);

            var inicioUtc = ConversaoFuso.ParaUtc(inicioLocal, p.Fuso);
            if (inicioUtc is null)
                continue;

            var fimUtc = inicioUtc.Value.AddMinutes(p.DuracaoMinutos);

            yield return new SlotDisponivel(SlotId.Calcular(p.TreinadorId, p.PacoteId, inicioUtc.Value), inicioUtc.Value, fimUtc);
        }
    }

    private static IEnumerable<DateTime> DiasNoIntervalo(DateTime fromUtc, DateTime toUtc, TimeZoneInfo fuso)
    {
        var primeiroDia = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, fuso).Date;
        var ultimoDia = TimeZoneInfo.ConvertTimeFromUtc(toUtc, fuso).Date;

        for (var dia = primeiroDia; dia <= ultimoDia; dia = dia.AddDays(1))
            yield return dia;
    }
}
