using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class VinculoAprovadoEmailHandler(
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    IEmailService emailService,
    ILogger<VinculoAprovadoEmailHandler> logger) : IDomainEventHandler<VinculoAprovadoEvent>
{
    public async Task HandleAsync(VinculoAprovadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (aluno is null)
        {
            logger.LogWarning("VinculoAprovadoEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (aluno.Email is null)
        {
            logger.LogDebug("VinculoAprovadoEmailHandler: aluno {Id} sem e-mail cadastrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        var nomeTreinador = treinador?.Nome ?? "seu treinador";

        await emailService.EnviarAsync(
            aluno.Email.Value,
            "Vínculo aprovado — forzion.tech",
            EmailTemplates.VinculoAprovado(aluno.Nome, nomeTreinador),
            cancellationToken).ConfigureAwait(false);
    }
}
