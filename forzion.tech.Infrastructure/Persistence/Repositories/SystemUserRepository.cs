using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class SystemUserRepository(AppDbContext context) : ISystemUserRepository
{
    private readonly AppDbContext _context = context;

    public async Task<SystemUser?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.SystemUsers
            .FirstOrDefaultAsync(su => su.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<SystemUser?> ObterPorSupabaseIdAsync(Guid supabaseId, CancellationToken cancellationToken = default) =>
        await _context.SystemUsers
            .FirstOrDefaultAsync(su => su.SupabaseId == supabaseId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(SystemUser systemUser, CancellationToken cancellationToken = default) =>
        await _context.SystemUsers.AddAsync(systemUser, cancellationToken).ConfigureAwait(false);
}
