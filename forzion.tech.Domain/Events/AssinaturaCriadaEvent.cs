namespace forzion.tech.Domain.Events;

public sealed record AssinaturaCriadaEvent(
    Guid AssinaturaId,
    Guid TreinadorId,
    Guid AlunoId,
    Guid PacoteAlunoId,
    decimal Valor,
    DateTime OcorridoEm) : IDomainEvent;
