using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AlunoInativadoEmailHandler(
    IAlunoRepository alunoRepository,
    IEmailService emailService,
    ILogger<AlunoInativadoEmailHandler> logger) : IDomainEventHandler<AlunoInativadoEvent>
{
    public async Task HandleAsync(AlunoInativadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (aluno is null)
        {
            logger.LogWarning("AlunoInativadoEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (aluno.Email is null)
        {
            logger.LogDebug("AlunoInativadoEmailHandler: aluno {Id} sem e-mail cadastrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        await emailService.EnviarAsync(
            aluno.Email.Value,
            "Conta inativada — forzion.tech",
            EmailTemplates.AlunoInativado(aluno.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
