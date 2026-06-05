namespace forzion.tech.Domain.Events;

public sealed record AssinaturaTreinadorReativadaEvent(
    Guid AssinaturaTreinadorId,
    Guid TreinadorId,
    DateTime OcorridoEm) : IDomainEvent;
