using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class TreinoDisponibilizadoEmailHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<TreinoDisponibilizadoEmailHandler> logger) : IDomainEventHandler<TreinoDisponibilizadoEvent>
{
    public async Task HandleAsync(TreinoDisponibilizadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!emailService.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("TreinoDisponibilizadoEmailHandler: aluno {Id} não encontrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(aluno.ContaId, cancellationToken)
            .ConfigureAwait(false);

        var emailDestino = aluno.Email?.Value ?? conta?.Email.Value;
        if (emailDestino is null)
        {
            logger.LogWarning("TreinoDisponibilizadoEmailHandler: aluno {Id} sem e-mail — ignorado.", domainEvent.AlunoId);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorAlunoAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        if (conta?.NotificacoesEngajamentoEmailOptOut == true) return;

        await emailService.EnviarAsync(
            emailDestino,
            "Novo treino disponível — forzion.tech",
            EmailTemplates.NovoTreinoDisponivel(aluno.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
