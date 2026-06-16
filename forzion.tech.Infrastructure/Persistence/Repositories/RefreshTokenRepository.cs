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

    // usado_em IS NULL resolve a corrida no lock de linha (READ COMMITTED): o concorrente bloqueia,
    // reavalia o predicado e afeta 0 linhas. Auto-commit (sem tx ambiente) ⇒ visível antes do sucessor.
    public async Task<int> MarcarUsadoSeNaoUsadoAsync(Guid tokenId, DateTime usadoEm, Guid sucessorId, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(t => t.Id == tokenId && t.UsadoEm == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.UsadoEm, usadoEm)
                .SetProperty(t => t.SubstituidoPorId, sucessorId), cancellationToken)
            .ConfigureAwait(false);
}
