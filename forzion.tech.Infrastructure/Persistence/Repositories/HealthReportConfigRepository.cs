using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class HealthReportConfigRepository(AppDbContext context) : IHealthReportConfigRepository
{
    public async Task<HealthReportConfig?> ObterAsync(CancellationToken cancellationToken = default) =>
        await context.HealthReportConfigs
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(HealthReportConfig config, CancellationToken cancellationToken = default) =>
        await context.HealthReportConfigs.AddAsync(config, cancellationToken).ConfigureAwait(false);
}
