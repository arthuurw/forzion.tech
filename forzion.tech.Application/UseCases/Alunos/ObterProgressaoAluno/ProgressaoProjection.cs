using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

public static class ProgressaoProjection
{
    public static List<ExercicioProgressao> Projetar(IReadOnlyList<ExecucaoDetalheItem> execucoes) =>
        execucoes
            .SelectMany(e => e.Exercicios.Select(ex => new
            {
                ex.NomeExercicio,
                ex.GrupoMuscular,
                Data = e.DataExecucao.Date,
                ex.CargaExecutada,
                ex.SeriesExecutadas,
                ex.RepeticoesExecutadas,
            }))
            .GroupBy(x => (x.NomeExercicio, x.GrupoMuscular))
            .Select(g => new ExercicioProgressao(
                g.Key.NomeExercicio,
                g.Key.GrupoMuscular,
                g.GroupBy(x => x.Data)
                    .OrderBy(d => d.Key)
                    .Select(d => new PontoProgressao(
                        d.Key,
                        d.Max(x => x.CargaExecutada),
                        (int)Math.Round(d.Average(x => x.SeriesExecutadas)),
                        (int)Math.Round(d.Average(x => x.RepeticoesExecutadas))))
                    .ToList()))
            .OrderBy(e => e.GrupoMuscular)
            .ThenBy(e => e.NomeExercicio)
            .ToList();
}
