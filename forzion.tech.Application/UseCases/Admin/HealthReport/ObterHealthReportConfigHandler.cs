using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public class ObterHealthReportConfigHandler(IHealthReportConfigRepository configRepository)
{
    public virtual async Task<HealthReportConfigResponse?> HandleAsync(CancellationToken cancellationToken = default)
    {
        var config = await configRepository.ObterAsync(cancellationToken).ConfigureAwait(false);
        return config is null ? null : HealthReportConfigResponseExtensions.ToResponse(config);
    }
}
