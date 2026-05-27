using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Health;

public class HealthReportSender(IEmailService emailService, ILogger<HealthReportSender> logger) : IHealthReportSender
{
    public async Task EnviarAsync(HealthReport report, IEnumerable<string> destinatarios, CancellationToken cancellationToken = default)
    {
        var assunto = $"[forzion.tech] Relatório de saúde — {report.Ambiente} ({report.StatusGeral})";
        var html = EmailTemplates.RelatorioSaude(report);

        foreach (var destinatario in destinatarios)
        {
            try
            {
                await emailService.EnviarAsync(destinatario, assunto, html, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao enviar relatório de saúde para {Destinatario}.", destinatario);
            }
        }
    }
}
