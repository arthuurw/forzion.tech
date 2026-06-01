using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

public class ObterProgressaoAlunoHandler(
    IExecucaoTreinoRepository execucaoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext)
{
    public virtual Task<ProgressaoAlunoResponse> HandleAsync(
        ObterProgressaoAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ProgressaoAlunoResponse> HandleAsyncCore(
        ObterProgressaoAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!userContext.IsSystemAdmin)
        {
            _ = await vinculoRepository
                .ObterAtivoAsync(userContext.PerfilId, query.AlunoId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new AcessoNegadoException();
        }

        var ate = query.Ate.Date.AddDays(1).AddTicks(-1);

        var rows = await execucaoRepository
            .ProjetarProgressaoAsync(query.AlunoId, query.De.Date, ate, cancellationToken)
            .ConfigureAwait(false);

        // Regroup in-memory: one ExercicioProgressao per (NomeExercicio, GrupoMuscular),
        // with one PontoProgressao per day. SQL already orders by grupoMuscular/nome/data.
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
