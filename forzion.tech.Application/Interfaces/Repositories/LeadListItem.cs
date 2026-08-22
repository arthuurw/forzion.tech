using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public sealed record LeadListItem(
    Guid Id,
    string Nome,
    TipoContatoLead ContatoTipo,
    string ContatoValor,
    LeadSource Source,
    LeadStatus Status,
    DateTime CreatedAt,
    bool Anonimizado);
