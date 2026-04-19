namespace forzion.tech.Domain.Events;

public sealed record TreinadorInativadoEvent(
    Guid TreinadorId,
    Guid InativadoPorId,
    DateTime OcorridoEm) : IDomainEvent;
