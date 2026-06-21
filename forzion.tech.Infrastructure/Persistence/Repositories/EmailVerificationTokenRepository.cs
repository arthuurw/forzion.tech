using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class EmailVerificationTokenRepository(AppDbContext context) : IEmailVerificationTokenRepository
{
    public async Task AdicionarAsync(EmailVerificationToken token, CancellationToken cancellationToken = default) =>
        await context.EmailVerificationTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task<EmailVerificationToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

    public async Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.EmailVerificationTokens
            .Where(t => t.ContaId == contaId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
