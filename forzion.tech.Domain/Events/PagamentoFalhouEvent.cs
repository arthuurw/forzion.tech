namespace forzion.tech.Domain.Events;

/// <summary>
/// Disparado em <see cref="Entities.AssinaturaAluno.RegistrarPagamentoFalho"/>.
///
/// Handler usa <see cref="TentativasFalhasConsecutivas"/> pra notificação progressiva:
/// - 1: e-mail "tente outro método"
/// - 2+: e-mail + WhatsApp escalado
/// - 3+: <see cref="AssinaturaAlunoMarcadaInadimplenteEvent"/> também é disparado
///       (transição Ativa → Inadimplente).
/// </summary>
public sealed record PagamentoFalhouEvent(
    Guid AssinaturaAlunoId,
    Guid AlunoId,
    int TentativasFalhasConsecutivas,
    DateTime OcorridoEm) : IDomainEvent;
