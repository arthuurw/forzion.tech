using forzion.tech.Domain.Enums;

namespace forzion.tech.Domain.Events;

public sealed record PagamentoTreinadorPagoEvent(
    Guid PagamentoTreinadorId,
    Guid TreinadorId,
    Guid AssinaturaTreinadorId,
    FinalidadePagamentoTreinador Finalidade,
    Guid? PlanoAlvoId,
    DateTime OcorridoEm) : IDomainEvent;
