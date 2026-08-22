using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Leads.ObterMetricas;

public record ObterMetricasLeadsResponse(
    int Total,
    int PorOrigemAgente,
    int PorOrigemManual,
    int Convertidos,
    double TaxaConversao,
    IReadOnlyDictionary<MotivoDescarteLead, int> PorMotivoDescarte);
