namespace forzion.tech.Domain.Events;

public sealed record VinculoAprovadoEvent(
    Guid VinculoId,
    Guid TreinadorId,
    Guid AlunoId,
    Guid AprovadoPorId,
    DateTime OcorridoEm) : IDomainEvent;
