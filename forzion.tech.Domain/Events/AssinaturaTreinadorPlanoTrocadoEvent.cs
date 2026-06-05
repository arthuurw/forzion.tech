namespace forzion.tech.Domain.Events;

public sealed record AssinaturaTreinadorPlanoTrocadoEvent(
    Guid AssinaturaTreinadorId,
    Guid TreinadorId,
    Guid PlanoAnteriorId,
    Guid PlanoNovoId,
    DateTime OcorridoEm) : IDomainEvent;
