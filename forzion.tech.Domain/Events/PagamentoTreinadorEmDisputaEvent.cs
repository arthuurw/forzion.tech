namespace forzion.tech.Domain.Events;

public sealed record PagamentoTreinadorEmDisputaEvent(
    Guid PagamentoTreinadorId,
    Guid TreinadorId,
    decimal Valor,
    DateTime OcorridoEm) : IDomainEvent;
