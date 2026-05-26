namespace forzion.tech.Domain.Events;

public sealed record AlunoRegistradoEvent(
    Guid AlunoId,
    Guid ContaId,
    string Nome,
    string? Email,
    DateTime OcorridoEm) : IDomainEvent;
