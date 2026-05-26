using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ExecucaoTreinoRepository(AppDbContext context) : IExecucaoTreinoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<IReadOnlyList<ExecucaoDetalheItem>> ListarPorAlunoComExerciciosAsync(
        Guid alunoId, DateTime de, DateTime ate, CancellationToken cancellationToken = default)
    {
        var execucoes = await _context.ExecucoesTreino
            .Where(e => e.AlunoId == alunoId && e.DataExecucao >= de && e.DataExecucao <= ate)
            .OrderBy(e => e.DataExecucao)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (execucoes.Count == 0)
            return [];

        var ids = execucoes.Select(e => e.Id).ToList();

        var exercicios = await (
            from ee in _context.ExecucoesExercicio
            join te in _context.TreinoExercicios on ee.TreinoExercicioId equals te.Id
            join ex in _context.Exercicios on te.ExercicioId equals ex.Id
            join gm in _context.GruposMusculares on ex.GrupoMuscularId equals gm.Id
            where ids.Contains(ee.ExecucaoTreinoId)
            select new
            {
                ee.ExecucaoTreinoId,
                ee.TreinoExercicioId,
                NomeExercicio = ex.Nome,
                GrupoMuscular = gm.Nome,
                ee.SeriesExecutadas,
                ee.RepeticoesExecutadas,
                ee.CargaExecutada,
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var porExecucao = exercicios
            .GroupBy(e => e.ExecucaoTreinoId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ExecucaoExercicioDetalhe>)g
                    .Select(e => new ExecucaoExercicioDetalhe(
                        e.TreinoExercicioId,
                        e.NomeExercicio,
                        e.GrupoMuscular,
                        e.SeriesExecutadas,
                        e.RepeticoesExecutadas,
                        e.CargaExecutada))
                    .ToList());

        return execucoes
            .Select(e => new ExecucaoDetalheItem(
                e.Id,
                e.DataExecucao,
                e.TreinoId,
                e.Observacao,
                porExecucao.TryGetValue(e.Id, out var exs) ? exs : []))
            .ToList();
    }

    public async Task AdicionarAsync(ExecucaoTreino execucao, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino.AddAsync(execucao, cancellationToken).ConfigureAwait(false);

    public async Task<bool> ExisteParaTreinoAsync(Guid treinoId, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .AnyAsync(e => e.TreinoId == treinoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> ExisteParaTreinoComAlunoAtivoAsync(Guid treinoId, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .AnyAsync(e => e.TreinoId == treinoId &&
                _context.TreinoAlunos.Any(ta => ta.TreinoId == treinoId &&
                    ta.AlunoId == e.AlunoId &&
                    ta.Status == Domain.Enums.TreinoAlunoStatus.Ativo),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ExecucaoTreino>> ListarPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .Where(e => e.AlunoId == alunoId)
            .OrderByDescending(e => e.DataExecucao)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ExecucaoComNome>> ListarComNomePorAlunoAsync(
        Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var items = await (
            from e in _context.ExecucoesTreino
            join t in _context.Treinos on e.TreinoId equals t.Id
            where e.AlunoId == alunoId
            orderby e.DataExecucao descending
            select new { e, NomeTreino = t.Nome }
        )
        .Skip((pagina - 1) * tamanhoPagina)
        .Take(tamanhoPagina)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

        if (items.Count == 0) return [];

        var ids = items.Select(x => x.e.Id).ToList();

        var stats = await (
            from ee in _context.ExecucoesExercicio
            where ids.Contains(ee.ExecucaoTreinoId)
            group ee by ee.ExecucaoTreinoId into g
            select new
            {
                ExecucaoId = g.Key,
                TotalExercicios = g.Count(),
                TotalSeries = g.Sum(x => x.SeriesExecutadas),
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var statsMap = stats.ToDictionary(x => x.ExecucaoId);

        return items.Select(x =>
        {
            var s = statsMap.TryGetValue(x.e.Id, out var found) ? found : null;
            return new ExecucaoComNome(
                x.e.Id, x.e.TreinoId, x.e.AlunoId, x.e.DataExecucao,
                x.e.Observacao, x.e.CreatedAt, x.NomeTreino,
                s?.TotalExercicios ?? 0, s?.TotalSeries ?? 0);
        }).ToList();
    }

    public async Task<int> ContarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .CountAsync(e => e.AlunoId == alunoId, cancellationToken)
            .ConfigureAwait(false);
}
