namespace forzion.tech.Domain.Events;

/// <summary>
/// Emitido quando um vínculo treinador↔aluno é criado em estado AguardandoAprovacao
/// (ex.: aluno se cadastra e fica pendente). Notifica o treinador (e-mail + WhatsApp)
/// de que há um novo aluno aguardando aprovação.
/// </summary>
public sealed record VinculoPendenteCriadoEvent(
    Guid VinculoId,
    Guid TreinadorId,
    Guid AlunoId,
    DateTime OcorridoEm) : IDomainEvent;
