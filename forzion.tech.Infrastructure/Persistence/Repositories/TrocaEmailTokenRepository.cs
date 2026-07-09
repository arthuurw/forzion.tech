using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TrocaEmailTokenRepository(AppDbContext context, TimeProvider timeProvider) : ITrocaEmailTokenRepository
{
    public async Task AdicionarAsync(TrocaEmailToken token, CancellationToken cancellationToken = default) =>
        await context.TrocaEmailTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task<TrocaEmailToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await context.TrocaEmailTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

    public async Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.TrocaEmailTokens
            .Where(t => t.ContaId == contaId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> LimparExpiradosAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        return await context.TrocaEmailTokens
            .Where(t => t.ExpiraEm <= agora || t.UsadoEm != null)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
