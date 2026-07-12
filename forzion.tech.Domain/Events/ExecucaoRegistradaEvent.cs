namespace forzion.tech.Domain.Events;

public sealed record ExecucaoRegistradaEvent(
    Guid AlunoId,
    Guid TreinoId,
    Guid ExecucaoTreinoId,
    DateTime OcorridoEm) : IDomainEvent;
