using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class GrupoMuscularRepository(AppDbContext context) : IGrupoMuscularRepository
{
    private readonly AppDbContext _context = context;

    public async Task<GrupoMuscular?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<GrupoMuscular>().FindAsync([id], cancellationToken);
    }

    public async Task<GrupoMuscular?> ObterPorNomeAsync(string nome, CancellationToken cancellationToken = default)
    {
        return await _context.Set<GrupoMuscular>()
            .FirstOrDefaultAsync(g => EF.Functions.ILike(g.Nome, nome), cancellationToken);
    }

    public async Task<IReadOnlyList<GrupoMuscular>> ListarTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<GrupoMuscular>()
            .AsNoTracking()
            .OrderBy(g => g.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<GrupoMuscular>().CountAsync(cancellationToken);
    }

    public async Task AdicionarAsync(GrupoMuscular grupoMuscular, CancellationToken cancellationToken = default)
    {
        await _context.Set<GrupoMuscular>().AddAsync(grupoMuscular, cancellationToken);
    }

    public void Excluir(GrupoMuscular grupoMuscular)
    {
        _context.Set<GrupoMuscular>().Remove(grupoMuscular);
    }
}
