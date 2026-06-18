namespace forzion.tech.Domain.Events;

public sealed record PagamentoTreinadorEstornadoEvent(
    Guid PagamentoTreinadorId,
    Guid TreinadorId,
    decimal Valor,
    DateTime OcorridoEm) : IDomainEvent;
