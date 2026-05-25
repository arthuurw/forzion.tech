using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AlunoRegistradoEmailHandler(
    IEmailService emailService,
    ILogger<AlunoRegistradoEmailHandler> logger) : IDomainEventHandler<AlunoRegistradoEvent>
{
    public async Task HandleAsync(AlunoRegistradoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        if (domainEvent.Email is null)
        {
            logger.LogDebug("AlunoRegistradoEmailHandler: aluno {Id} sem e-mail cadastrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        await emailService.EnviarAsync(
            domainEvent.Email,
            "Bem-vindo à forzion.tech!",
            EmailTemplates.BemVindoAluno(domainEvent.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
