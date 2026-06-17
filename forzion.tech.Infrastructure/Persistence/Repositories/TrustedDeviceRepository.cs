using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TrustedDeviceRepository(AppDbContext context, TimeProvider timeProvider) : ITrustedDeviceRepository
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

    public async Task RemoverPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default)
    {
        var devices = await context.TrustedDevices
            .Where(d => d.ContaId == contaId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.TrustedDevices.RemoveRange(devices);
    }

    public async Task<int> LimparExpiradosAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        return await context.TrustedDevices
            .Where(d => d.ExpiraEm <= agora || d.RevogadoEm != null)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
