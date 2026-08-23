using forzion.tech.Domain.Entities;

namespace forzion.tech.Domain.Services;

public static class DerivadorDisponibilidade
{
    public static IReadOnlyList<SlotDisponivel> Derivar(ParametrosDerivacao p)
    {
        var candidatos = new List<SlotDisponivel>();

        foreach (var diaLocal in DiasNoIntervalo(p.From, p.To, p.Fuso))
        {
            var diaSemana = (int)diaLocal.DayOfWeek;
            var horariosDoDia = p.Horarios.Where(h => h.DiaSemana == diaSemana);

            foreach (var horario in horariosDoDia)
                candidatos.AddRange(BlocosDoTurno(p, diaLocal, horario));
        }

        return candidatos
            .Where(s => !p.Bloqueios.Any(b => b.Cobre(s.InicioUtc, s.FimUtc, p.Fuso)))
            .DistinctBy(s => s.InicioUtc)
            .OrderBy(s => s.InicioUtc)
            .ToList();
    }

    private static IEnumerable<SlotDisponivel> BlocosDoTurno(ParametrosDerivacao p, DateTime diaLocal, HorarioFuncionamento horario)
    {
        var abreMinutos = horario.AbreAs.Hour * 60 + horario.AbreAs.Minute;
        var fechaMinutos = horario.FechaAs.Hour * 60 + horario.FechaAs.Minute;

        for (var inicioMinutos = abreMinutos; inicioMinutos + p.DuracaoMinutos <= fechaMinutos; inicioMinutos += p.DuracaoMinutos)
        {
            var inicioLocal = diaLocal.AddMinutes(inicioMinutos);
            var fimLocal = diaLocal.AddMinutes(inicioMinutos + p.DuracaoMinutos);

            var inicioUtc = ConversaoFuso.ParaUtc(inicioLocal, p.Fuso);
            var fimUtc = ConversaoFuso.ParaUtc(fimLocal, p.Fuso);
            if (inicioUtc is null || fimUtc is null)
                continue;

            yield return new SlotDisponivel(SlotId.Calcular(p.TreinadorId, p.PacoteId, inicioUtc.Value), inicioUtc.Value, fimUtc.Value);
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
