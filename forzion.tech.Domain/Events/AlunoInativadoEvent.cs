namespace forzion.tech.Domain.Events;

public sealed record AlunoInativadoEvent(
    Guid AlunoId,
    DateTime OcorridoEm) : IDomainEvent;
