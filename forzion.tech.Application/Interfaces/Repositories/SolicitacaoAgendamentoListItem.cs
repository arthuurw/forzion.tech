using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public sealed record SolicitacaoAgendamentoListItem(
    Guid Id,
    Guid PacoteId,
    string PacoteNome,
    DateTime InicioUtc,
    DateTime FimUtc,
    SolicitacaoAgendamentoStatus Status,
    string? Motivo,
    DateTime CreatedAt,
    Guid LeadId,
    string LeadNome,
    TipoContatoLead LeadContatoTipo,
    string LeadContatoValor,
    bool LeadAnonimizado);
