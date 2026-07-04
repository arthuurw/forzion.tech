namespace forzion.tech.Domain.Events;

public sealed record TreinoDisponibilizadoEvent(
    Guid AlunoId,
    Guid TreinoId,
    Guid TreinoAlunoId,
    DateTime OcorridoEm) : IDomainEvent;
