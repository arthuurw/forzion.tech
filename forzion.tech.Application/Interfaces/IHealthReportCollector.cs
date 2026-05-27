using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces;

public interface IHealthReportCollector
{
    Task<HealthReport> ColetarAsync(HealthReportConfig config, CancellationToken cancellationToken = default);
}
