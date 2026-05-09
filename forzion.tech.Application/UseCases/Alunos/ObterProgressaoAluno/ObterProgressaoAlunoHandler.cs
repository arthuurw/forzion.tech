using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

public class ObterProgressaoAlunoHandler(
    IExecucaoTreinoRepository execucaoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext)
{
    private readonly IExecucaoTreinoRepository _execucaoRepository = execucaoRepository;
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
    private readonly IUserContext _userContext = userContext;

    public virtual async Task<ProgressaoAlunoResponse> HandleAsync(
        ObterProgressaoAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _vinculoRepository
            .ObterAtivoAsync(_userContext.PerfilId, query.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AcessoNegadoException();

        var ate = query.Ate.Date.AddDays(1).AddTicks(-1);

        var execucoes = await _execucaoRepository
            .ListarPorAlunoComExerciciosAsync(query.AlunoId, query.De.Date, ate, cancellationToken)
            .ConfigureAwait(false);

        var exercicios = execucoes
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

        return new ProgressaoAlunoResponse(exercicios);
    }
}
