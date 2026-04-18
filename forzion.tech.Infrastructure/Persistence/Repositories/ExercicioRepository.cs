using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ExercicioRepository(AppDbContext context) : IExercicioRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Exercicio?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Exercicios
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Exercicio> Items, int Total)> ListarAsync(
        Guid? treinadorId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var query = _context.Exercicios
            .Where(e => e.TreinadorId == null || e.TreinadorId == treinadorId)
            .OrderBy(e => e.Nome);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task AdicionarAsync(Exercicio exercicio, CancellationToken cancellationToken = default) =>
        await _context.Exercicios.AddAsync(exercicio, cancellationToken).ConfigureAwait(false);

    public async Task<bool> ExisteAsync(Guid id, Guid? treinadorId, CancellationToken cancellationToken = default) =>
        await _context.Exercicios
            .AnyAsync(e => e.Id == id && (e.TreinadorId == null || e.TreinadorId == treinadorId), cancellationToken)
            .ConfigureAwait(false);
}
