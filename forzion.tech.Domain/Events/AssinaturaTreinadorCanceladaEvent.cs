namespace forzion.tech.Domain.Events;

public sealed record AssinaturaTreinadorCanceladaEvent(
    Guid AssinaturaTreinadorId,
    Guid TreinadorId,
    DateTime OcorridoEm) : IDomainEvent;
