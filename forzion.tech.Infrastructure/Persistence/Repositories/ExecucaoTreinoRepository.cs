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
            where ids.Contains(ee.ExecucaoTreinoId)
            select new
            {
                ee.ExecucaoTreinoId,
                ee.TreinoExercicioId,
                NomeExercicio = ex.Nome,
                GrupoMuscular = ex.GrupoMuscular.ToString(),
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

    public async Task<IReadOnlyList<ExecucaoTreino>> ListarPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .Where(e => e.AlunoId == alunoId)
            .OrderByDescending(e => e.DataExecucao)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> ContarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .CountAsync(e => e.AlunoId == alunoId, cancellationToken)
            .ConfigureAwait(false);
}
