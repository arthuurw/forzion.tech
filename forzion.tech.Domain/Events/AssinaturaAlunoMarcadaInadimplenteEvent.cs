namespace forzion.tech.Domain.Events;

/// <summary>
/// Disparado quando uma <see cref="Entities.AssinaturaAluno"/> atinge o limite
/// de tentativas falhas consecutivas e transiciona de Ativa → Inadimplente.
///
/// Handlers notificam aluno + treinador. Backend filter <c>RequireAssinaturaAtivaFilter</c>
/// passa a bloquear endpoints de "consumo" (criar nova execução, etc) — leitura
/// continua liberada.
/// </summary>
public sealed record AssinaturaAlunoMarcadaInadimplenteEvent(
    Guid AssinaturaAlunoId,
    Guid AlunoId,
    Guid TreinadorId,
    int TentativasFalhasConsecutivas,
    DateTime OcorridoEm) : IDomainEvent;
