using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class HealthSnapshotRepository(AppDbContext context) : IHealthSnapshotRepository
{
    public async Task AdicionarAsync(HealthSnapshot snapshot, CancellationToken cancellationToken = default) =>
        await context.HealthSnapshots.AddAsync(snapshot, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<HealthSnapshot>> ListarRecentesAsync(int limite, CancellationToken cancellationToken = default) =>
        await context.HealthSnapshots
            .OrderByDescending(s => s.CapturadoEm)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
