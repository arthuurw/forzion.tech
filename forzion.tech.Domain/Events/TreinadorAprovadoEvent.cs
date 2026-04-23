namespace forzion.tech.Domain.Events;

public sealed record TreinadorAprovadoEvent(
    Guid TreinadorId,
    Guid AprovadoPorId,
    DateTime OcorridoEm) : IDomainEvent;
