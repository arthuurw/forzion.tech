using forzion.tech.Domain.Enums;

namespace forzion.tech.Domain.Events;

public sealed record ContaAnonimizadaEvent(
    Guid ContaId,
    TipoConta TipoConta,
    DateTime OcorridoEm) : IDomainEvent;
