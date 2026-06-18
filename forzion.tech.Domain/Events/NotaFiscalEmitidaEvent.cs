namespace forzion.tech.Domain.Events;

public sealed record NotaFiscalEmitidaEvent(
    Guid NotaFiscalId,
    Guid TreinadorId,
    string ChaveAcesso,
    DateTime OcorridoEm) : IDomainEvent;
