namespace forzion.tech.Domain.Events;

public sealed record CobrancaProximaAlunoEvent(
    Guid AssinaturaAlunoId,
    Guid AlunoId,
    Guid TreinadorId,
    decimal Valor,
    DateTime DataProximaCobranca,
    DateTime OcorridoEm) : IDomainEvent;
