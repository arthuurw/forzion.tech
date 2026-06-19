using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository(AppDbContext context) : IPasswordResetTokenRepository
{
    public async Task AdicionarAsync(PasswordResetToken token, CancellationToken cancellationToken = default) =>
        await context.PasswordResetTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task<PasswordResetToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

    public async Task InvalidarPendentesPorContaAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default) =>
        await context.PasswordResetTokens
            .Where(t => t.ContaId == contaId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, agora), cancellationToken)
            .ConfigureAwait(false);
}
