using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TenantRepository(AppDbContext context) : ITenantRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Tenant?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Tenants
            .Include(t => t.Plano)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> SlugExisteAsync(Slug slug, CancellationToken cancellationToken = default) =>
        await _context.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
        await _context.Tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
}
