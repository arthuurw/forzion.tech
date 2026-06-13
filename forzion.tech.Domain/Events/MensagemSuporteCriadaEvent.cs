using forzion.tech.Domain.Enums;

namespace forzion.tech.Domain.Events;

public sealed record MensagemSuporteCriadaEvent(
    Guid MensagemSuporteId,
    Guid ContaId,
    CategoriaSuporte Categoria,
    string Assunto,
    string Descricao,
    DateTime OcorridoEm) : IDomainEvent;
