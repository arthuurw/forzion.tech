using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ContaMfaRepository(AppDbContext context) : IContaMfaRepository
{
    public async Task AdicionarAsync(ContaMfa mfa, CancellationToken cancellationToken = default) =>
        await context.ContasMfa.AddAsync(mfa, cancellationToken).ConfigureAwait(false);

    public async Task<ContaMfa?> BuscarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.ContasMfa
            .FirstOrDefaultAsync(m => m.ContaId == contaId, cancellationToken)
            .ConfigureAwait(false);
}
