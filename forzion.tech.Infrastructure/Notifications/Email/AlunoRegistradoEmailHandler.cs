using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AlunoRegistradoEmailHandler(
    IContaRepository contaRepository,
    IEmailService emailService,
    ILogger<AlunoRegistradoEmailHandler> logger) : IDomainEventHandler<AlunoRegistradoEvent>
{
    public async Task HandleAsync(AlunoRegistradoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var emailDestino = domainEvent.Email;
        if (emailDestino is null)
        {
            var conta = await contaRepository.ObterPorIdAsync(domainEvent.ContaId, cancellationToken).ConfigureAwait(false);
            if (conta is null)
            {
                logger.LogWarning("AlunoRegistradoEmailHandler: conta {ContaId} não encontrada para aluno {AlunoId}.", domainEvent.ContaId, domainEvent.AlunoId);
                return;
            }
            emailDestino = conta.Email.Value;
        }

        await emailService.EnviarAsync(
            emailDestino,
            "Bem-vindo à forzion.tech!",
            EmailTemplates.BemVindoAluno(domainEvent.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
