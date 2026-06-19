using forzion.tech.Domain.Enums;

namespace forzion.tech.Domain.Events;

public sealed record EmailCriticoSolicitadoEvent(
    Guid Id,
    EmailCriticoTemplate Template,
    string DadosCifrados,
    DateTime OcorridoEm) : IDomainEvent;
