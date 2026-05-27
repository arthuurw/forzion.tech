using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IHealthReportConfigRepository
{
    Task<HealthReportConfig?> ObterAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(HealthReportConfig config, CancellationToken cancellationToken = default);
}
