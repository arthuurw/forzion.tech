using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AlunoInativadoEmailHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<AlunoInativadoEmailHandler> logger) : IDomainEventHandler<AlunoInativadoEvent>
{
    public async Task HandleAsync(AlunoInativadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var canais = await planoNotificationPolicy
            .ResolverPorAlunoAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (aluno is null)
        {
            logger.LogWarning("AlunoInativadoEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        string? emailDestino = aluno.Email?.Value;
        if (emailDestino is null)
        {
            var conta = await contaRepository.ObterPorIdAsync(aluno.ContaId, cancellationToken).ConfigureAwait(false);
            emailDestino = conta?.Email.Value;
        }

        if (emailDestino is null)
        {
            logger.LogWarning("AlunoInativadoEmailHandler: aluno {Id} sem e-mail — ignorado.", domainEvent.AlunoId);
            return;
        }

        await emailService.EnviarAsync(
            emailDestino,
            "Conta inativada — forzion.tech",
            EmailTemplates.AlunoInativado(aluno.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
