namespace forzion.tech.Domain.Events;

public sealed record CobrancaProximaTreinadorEvent(
    Guid AssinaturaTreinadorId,
    Guid TreinadorId,
    decimal Valor,
    DateTime DataProximaCobranca,
    DateTime OcorridoEm) : IDomainEvent;
