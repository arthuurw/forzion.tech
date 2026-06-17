using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class MfaRecoveryCodeRepository(AppDbContext context) : IMfaRecoveryCodeRepository
{
    public async Task AdicionarRangeAsync(IEnumerable<MfaRecoveryCode> codes, CancellationToken cancellationToken = default) =>
        await context.MfaRecoveryCodes.AddRangeAsync(codes, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MfaRecoveryCode>> ListarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.MfaRecoveryCodes
            .Where(c => c.ContaId == contaId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task RemoverPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default)
    {
        var codes = await context.MfaRecoveryCodes
            .Where(c => c.ContaId == contaId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.MfaRecoveryCodes.RemoveRange(codes);
    }
}
