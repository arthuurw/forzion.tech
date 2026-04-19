using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinadorRepository(AppDbContext context) : ITreinadorRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Treinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Treinadores
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Treinador?> ObterPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await _context.Treinadores
            .FirstOrDefaultAsync(t => t.ContaId == contaId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Treinador>> ListarAtivosAsync(CancellationToken cancellationToken = default) =>
        await _context.Treinadores
            .Where(t => t.Status == TreinadorStatus.Ativo)
            .OrderBy(t => t.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Treinador> Items, int Total)> ListarAsync(
        TreinadorStatus? status, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var query = _context.Treinadores.AsQueryable();
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(t => t.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task AdicionarAsync(Treinador treinador, CancellationToken cancellationToken = default) =>
        await _context.Treinadores.AddAsync(treinador, cancellationToken).ConfigureAwait(false);
}
