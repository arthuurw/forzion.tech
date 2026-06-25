using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Alunos.Dashboard;

public static class SessoesPorSemanaCalculator
{
    public static IReadOnlyList<SessaoSemanaItem> Bucketizar(
        DateTime agora, int semanas, IEnumerable<SessaoDiaCount> dias)
    {
        var ate = agora.Date;
        var de = ate.AddDays(-7 * semanas);

        var totais = new int[semanas];
        foreach (var dia in dias)
        {
            var offset = (dia.Dia.Date - de).Days;
            if (offset < 1 || offset > 7 * semanas) continue;
            totais[(offset - 1) / 7] += dia.Total;
        }

        var buckets = new List<SessaoSemanaItem>(semanas);
        for (var j = 0; j < semanas; j++)
            buckets.Add(new SessaoSemanaItem(de.AddDays((7 * j) + 1), de.AddDays(7 * (j + 1)), totais[j]));

        return buckets;
    }
}
