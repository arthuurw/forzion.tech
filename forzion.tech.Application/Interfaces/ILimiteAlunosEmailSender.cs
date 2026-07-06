namespace forzion.tech.Application.Interfaces;

// Ungated: notificações de graça de limite de alunos são billing/account-críticas, enviadas
// independente do tier efetivo do treinador (mesmo Free) — não passam por IPlanoNotificationPolicy.
public interface ILimiteAlunosEmailSender
{
    Task EnviarInicioAsync(Guid contaId, string nomeTreinador, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default);

    Task EnviarLembreteAsync(Guid contaId, string nomeTreinador, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default);

    Task EnviarAplicadoAsync(Guid contaId, string nomeTreinador, int quantidadeDesativada, CancellationToken cancellationToken = default);
}
