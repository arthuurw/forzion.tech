namespace forzion.tech.Domain.Events;

/// <summary>
/// Disparado quando uma <see cref="Entities.AssinaturaAluno"/> transiciona de
/// Inadimplente → Ativa via <c>RegistrarPagamentoRegularizado</c> (pagamento regularizado
/// pelo aluno após inadimplência).
///
/// Handlers notificam aluno + treinador.
/// </summary>
public sealed record AssinaturaAlunoReativadaEvent(
    Guid AssinaturaAlunoId,
    Guid AlunoId,
    Guid TreinadorId,
    DateTime OcorridoEm) : IDomainEvent;
