using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IHealthSnapshotRepository
{
    Task AdicionarAsync(HealthSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthSnapshot>> ListarRecentesAsync(int limite, CancellationToken cancellationToken = default);
}
