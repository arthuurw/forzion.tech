using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TrustedDeviceRepository(AppDbContext context) : ITrustedDeviceRepository
{
    public async Task AdicionarAsync(TrustedDevice device, CancellationToken cancellationToken = default) =>
        await context.TrustedDevices.AddAsync(device, cancellationToken).ConfigureAwait(false);

    public async Task<TrustedDevice?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await context.TrustedDevices
            .FirstOrDefaultAsync(d => d.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TrustedDevice>> ListarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.TrustedDevices
            .Where(d => d.ContaId == contaId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
