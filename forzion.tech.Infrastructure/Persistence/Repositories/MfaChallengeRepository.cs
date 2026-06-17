using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class MfaChallengeRepository(AppDbContext context) : IMfaChallengeRepository
{
    public async Task AdicionarAsync(MfaChallenge challenge, CancellationToken cancellationToken = default) =>
        await context.MfaChallenges.AddAsync(challenge, cancellationToken).ConfigureAwait(false);

    public async Task<MfaChallenge?> BuscarUltimoPorContaEPropositoAsync(Guid contaId, MfaProposito proposito, CancellationToken cancellationToken = default) =>
        await context.MfaChallenges
            .Where(c => c.ContaId == contaId && c.Proposito == proposito)
            .OrderByDescending(c => c.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
