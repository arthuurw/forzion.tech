namespace forzion.tech.Domain.Events;

public sealed record AssinaturaTreinadorCriadaEvent(
    Guid AssinaturaTreinadorId,
    Guid TreinadorId,
    Guid PlanoPlataformaId,
    decimal Valor,
    DateTime OcorridoEm) : IDomainEvent;
