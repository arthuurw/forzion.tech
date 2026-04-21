namespace forzion.tech.Domain.Events;

public sealed record TreinadorReprovadoEvent(
    Guid TreinadorId,
    Guid ReprovadoPorId,
    DateTime OcorridoEm) : IDomainEvent;
