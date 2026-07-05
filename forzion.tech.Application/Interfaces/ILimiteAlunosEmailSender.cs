namespace forzion.tech.Application.Interfaces;

// Ungated (NOTIF-04): notificações de graça de limite de alunos são billing/account-críticas,
// enviadas independente do tier efetivo do treinador (mesmo Free) — não passam por IPlanoNotificationPolicy.
public interface ILimiteAlunosEmailSender
{
    Task EnviarInicioAsync(Guid treinadorId, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default);

    Task EnviarLembreteAsync(Guid treinadorId, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default);

    Task EnviarAplicadoAsync(Guid treinadorId, int quantidadeDesativada, CancellationToken cancellationToken = default);
}
