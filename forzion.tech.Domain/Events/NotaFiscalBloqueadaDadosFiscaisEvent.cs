namespace forzion.tech.Domain.Events;

public sealed record NotaFiscalBloqueadaDadosFiscaisEvent(
    Guid NotaFiscalId,
    Guid TreinadorId,
    DateTime OcorridoEm) : IDomainEvent;
