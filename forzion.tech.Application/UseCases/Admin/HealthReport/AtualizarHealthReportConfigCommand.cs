namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public record AtualizarHealthReportConfigCommand(
    bool Ativo,
    TimeOnly HoraEnvioUtc,
    IReadOnlyList<string> Destinatarios,
    bool IncluirLiveness,
    bool IncluirKpis,
    bool IncluirEntregabilidade,
    bool IncluirErros);
