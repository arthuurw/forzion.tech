namespace forzion.tech.Domain.Events;

public sealed record ContaRegistradaEvent(
    Guid ContaId,
    string Email,
    DateTime OcorridoEm) : IDomainEvent;
