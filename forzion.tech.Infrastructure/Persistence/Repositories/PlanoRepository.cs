using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PlanoRepository(AppDbContext context) : IPlanoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Plano?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Planos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<Plano?> ObterPlanoFreeAsync(CancellationToken cancellationToken = default) =>
        await _context.Planos.FirstOrDefaultAsync(p => p.IsFree, cancellationToken).ConfigureAwait(false);
}
