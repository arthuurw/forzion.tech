using forzion.tech.Application.UseCases.Admin.HealthReport;

namespace forzion.tech.Application.Interfaces;

public interface IHealthReportSender
{
    Task EnviarAsync(HealthReport report, IEnumerable<string> destinatarios, CancellationToken cancellationToken = default);
}
