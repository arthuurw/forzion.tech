namespace forzion.tech.Domain.Events;

public sealed record AssinaturaAlunoCriadaEvent(
    Guid AssinaturaAlunoId,
    Guid TreinadorId,
    Guid AlunoId,
    Guid PacoteId,
    decimal Valor,
    DateTime OcorridoEm) : IDomainEvent;
