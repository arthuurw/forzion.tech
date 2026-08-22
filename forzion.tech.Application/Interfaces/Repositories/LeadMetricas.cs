using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public sealed record LeadMetricas(
    int Total,
    int PorOrigemAgente,
    int PorOrigemManual,
    int Convertidos,
    IReadOnlyDictionary<MotivoDescarteLead, int> PorMotivoDescarte);
