using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task AdicionarAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task<RefreshToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> MarcarUsadoSeNaoUsadoAsync(Guid tokenId, DateTime usadoEm, Guid sucessorId, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(t => t.Id == tokenId && t.UsadoEm == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.UsadoEm, usadoEm)
                .SetProperty(t => t.SubstituidoPorId, sucessorId), cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> RotacionarAtomicoAsync(Guid tokenId, DateTime usadoEm, RefreshToken sucessor, CancellationToken cancellationToken = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var afetadas = await MarcarUsadoSeNaoUsadoAsync(tokenId, usadoEm, sucessor.Id, cancellationToken).ConfigureAwait(false);
        if (afetadas == 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await context.RefreshTokens.AddAsync(sucessor, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
