namespace forzion.tech.Domain.Events;

public sealed record AssinaturaTreinadorMarcadaInadimplenteEvent(
    Guid AssinaturaTreinadorId,
    Guid TreinadorId,
    int TentativasFalhasConsecutivas,
    DateTime OcorridoEm) : IDomainEvent;
