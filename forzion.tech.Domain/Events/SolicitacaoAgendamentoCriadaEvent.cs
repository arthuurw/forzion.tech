namespace forzion.tech.Domain.Events;

public sealed record SolicitacaoAgendamentoCriadaEvent(
    Guid SolicitacaoId,
    Guid TreinadorId,
    Guid PacoteId,
    DateTime InicioUtc,
    DateTime OcorridoEm) : IDomainEvent;
