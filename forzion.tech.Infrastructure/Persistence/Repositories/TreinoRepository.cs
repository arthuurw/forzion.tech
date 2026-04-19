using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinoRepository(AppDbContext context) : ITreinoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Treino?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Treinos
            .Include(t => t.Exercicios).ThenInclude(te => te.Exercicio)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Treino> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Treinos
            .Where(t => t.TreinadorId == treinadorId);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await baseQuery
            .Include(t => t.Exercicios).ThenInclude(te => te.Exercicio)
            .OrderBy(t => t.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<(IReadOnlyList<Treino> Items, int Total)> ListarPorAlunoAsync(
        Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var treinoIds = _context.TreinoAlunos
            .Where(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo)
            .Select(ta => ta.TreinoId);

        var baseQuery = _context.Treinos
            .Where(t => treinoIds.Contains(t.Id));

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await baseQuery
            .Include(t => t.Exercicios).ThenInclude(te => te.Exercicio)
            .OrderBy(t => t.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task AdicionarAsync(Treino treino, CancellationToken cancellationToken = default) =>
        await _context.Treinos.AddAsync(treino, cancellationToken).ConfigureAwait(false);
}
