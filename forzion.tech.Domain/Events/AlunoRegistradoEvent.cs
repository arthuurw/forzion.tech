namespace forzion.tech.Domain.Events;

public sealed record AlunoRegistradoEvent(
    Guid AlunoId,
    string Nome,
    string? Email,
    DateTime OcorridoEm) : IDomainEvent;
