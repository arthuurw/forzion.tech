using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ExecucaoTreinoRepository(AppDbContext context) : IExecucaoTreinoRepository
{
    private readonly AppDbContext _context = context;

    public async Task AdicionarAsync(ExecucaoTreino execucao, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino.AddAsync(execucao, cancellationToken).ConfigureAwait(false);

    public async Task<ExecucaoTreino?> ObterPorIdempotencyKeyAsync(Guid alunoId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .FirstOrDefaultAsync(e => e.AlunoId == alunoId && e.IdempotencyKey == idempotencyKey, cancellationToken)
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
            .AsNoTracking()
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

    public async Task<IReadOnlyList<ProgressaoAggRow>> ProjetarProgressaoAsync(
        Guid alunoId, DateTime de, DateTime ate, CancellationToken cancellationToken = default)
    {
        // Push GROUP BY to SQL: one row per (exercício, grupoMuscular, data).
        // Aggregação em SQL evita hidratar cada execução.
        // Npgsql exige Kind=Utc para comparar com colunas timestamptz (handlers passam
        // DateTime.UtcNow.Date, que vem com Kind=Unspecified).
        // O bucket `DataExecucao.Date` é UTC-determinístico: em coluna timestamptz (modo
        // não-legacy) o Npgsql traduz `.Date` para `date_trunc('day', x, 'UTC')`, NÃO no TZ
        // da sessão. Não trocar por cast a date (`::date` seria TZ-dependente).
        de = DateTime.SpecifyKind(de, DateTimeKind.Utc);
        ate = DateTime.SpecifyKind(ate, DateTimeKind.Utc);

        var rows = await (
            from e in _context.ExecucoesTreino
            join ee in _context.ExecucoesExercicio on e.Id equals ee.ExecucaoTreinoId
            join te in _context.TreinoExercicios on ee.TreinoExercicioId equals te.Id
            join ex in _context.Exercicios on te.ExercicioId equals ex.Id
            join gm in _context.GruposMusculares on ex.GrupoMuscularId equals gm.Id
            where e.AlunoId == alunoId && e.DataExecucao >= de && e.DataExecucao <= ate
            group new { ee.CargaExecutada, ee.SeriesExecutadas, ee.RepeticoesExecutadas }
                by new { NomeExercicio = ex.Nome, GrupoMuscular = gm.Nome, Data = e.DataExecucao.Date }
            into g
            orderby g.Key.GrupoMuscular, g.Key.NomeExercicio, g.Key.Data
            select new ProgressaoAggRow(
                g.Key.NomeExercicio,
                g.Key.GrupoMuscular,
                g.Key.Data,
                g.Max(x => x.CargaExecutada),
                g.Average(x => (double)x.SeriesExecutadas),
                g.Average(x => (double)x.RepeticoesExecutadas))
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows;
    }

    public async Task AnonimizarObservacoesPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .Where(e => e.AlunoId == alunoId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.Observacao, (string?)null),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task ExcluirPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .Where(e => e.AlunoId == alunoId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
}
