using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public class ListarHealthSnapshotsHandler(IHealthSnapshotRepository snapshotRepository)
{
    private const int LimitePadrao = 30;

    public virtual async Task<IReadOnlyList<HealthSnapshotResponse>> HandleAsync(
        int? limite = null,
        CancellationToken cancellationToken = default)
    {
        var quantidade = limite is > 0 ? limite.Value : LimitePadrao;
        var snapshots = await snapshotRepository.ListarRecentesAsync(quantidade, cancellationToken).ConfigureAwait(false);
        return snapshots.Select(HealthSnapshotResponseExtensions.ToResponse).ToList();
    }
}
