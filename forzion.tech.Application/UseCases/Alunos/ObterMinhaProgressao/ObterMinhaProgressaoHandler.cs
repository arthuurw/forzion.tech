using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

namespace forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;

public class ObterMinhaProgressaoHandler(
    IExecucaoTreinoRepository execucaoRepository,
    IUserContext userContext)
{
    public virtual async Task<ProgressaoAlunoResponse> HandleAsync(
        DateTime de,
        DateTime ate,
        CancellationToken cancellationToken = default)
    {
        var alunoId = userContext.PerfilId;
        var ateInclusive = ate.Date.AddDays(1).AddTicks(-1);

        var rows = await execucaoRepository
            .ProjetarProgressaoAsync(alunoId, de.Date, ateInclusive, cancellationToken)
            .ConfigureAwait(false);

        var exercicios = rows
            .GroupBy(r => (r.NomeExercicio, r.GrupoMuscular))
            .Select(g => new ExercicioProgressao(
                g.Key.NomeExercicio,
                g.Key.GrupoMuscular,
                g.OrderBy(r => r.Data)
                    .Select(r => new PontoProgressao(
                        r.Data,
                        r.CargaMaxima,
                        (int)Math.Round(r.MediaSeries),
                        (int)Math.Round(r.MediaRepeticoes)))
                    .ToList()))
            .OrderBy(e => e.GrupoMuscular)
            .ThenBy(e => e.NomeExercicio)
            .ToList();

        return new ProgressaoAlunoResponse(exercicios);
    }
}
