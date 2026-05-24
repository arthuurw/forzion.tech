namespace forzion.tech.Domain.Events;

public sealed record AlunoAtualizadoEvent(
    Guid AlunoId,
    string Nome,
    string? Email,
    DateTime OcorridoEm) : IDomainEvent;
