namespace forzion.tech.Domain.Events;

public sealed record AssinaturaTreinadorPagamentoFalhouEvent(
    Guid AssinaturaTreinadorId,
    Guid TreinadorId,
    int TentativasFalhasConsecutivas,
    DateTime OcorridoEm) : IDomainEvent;
