using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public record HealthReportConfigResponse(
    Guid Id,
    bool Ativo,
    TimeOnly HoraEnvioUtc,
    IReadOnlyList<string> Destinatarios,
    bool IncluirLiveness,
    bool IncluirKpis,
    bool IncluirEntregabilidade,
    bool IncluirErros,
    DateTime? UltimoEnvioEm);

public static class HealthReportConfigResponseExtensions
{
    public static HealthReportConfigResponse ToResponse(HealthReportConfig config) => new(
        config.Id,
        config.Ativo,
        config.HoraEnvioUtc,
        config.ObterDestinatarios(),
        config.IncluirLiveness,
        config.IncluirKpis,
        config.IncluirEntregabilidade,
        config.IncluirErros,
        config.UltimoEnvioEm);
}
