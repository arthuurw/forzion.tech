using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class RefreshTokenFamilyRepository(AppDbContext context) : IRefreshTokenFamilyRepository
{
    public async Task AdicionarAsync(RefreshTokenFamily familia, CancellationToken cancellationToken = default) =>
        await context.RefreshTokenFamilies.AddAsync(familia, cancellationToken).ConfigureAwait(false);

    public async Task<RefreshTokenFamily?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.RefreshTokenFamilies
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<RefreshTokenFamily>> ListarAtivasPorContaAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default) =>
        await context.RefreshTokenFamilies
            .Where(f => f.ContaId == contaId && f.RevogadaEm == null && f.AbsolutoExpiraEm > agora)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> LimparExpiradasAsync(DateTime agora, CancellationToken cancellationToken = default) =>
        await context.RefreshTokenFamilies
            .Where(f => f.RevogadaEm != null || f.AbsolutoExpiraEm <= agora)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.RefreshTokenFamilies
            .Where(f => f.ContaId == contaId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
