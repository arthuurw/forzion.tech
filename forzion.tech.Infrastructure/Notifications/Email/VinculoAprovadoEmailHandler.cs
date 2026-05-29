using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class VinculoAprovadoEmailHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    ITreinadorRepository treinadorRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<VinculoAprovadoEmailHandler> logger) : IDomainEventHandler<VinculoAprovadoEvent>
{
    public async Task HandleAsync(VinculoAprovadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (aluno is null)
        {
            logger.LogWarning("VinculoAprovadoEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
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
            logger.LogWarning("VinculoAprovadoEmailHandler: aluno {Id} sem e-mail — ignorado.", domainEvent.AlunoId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        var nomeTreinador = treinador?.Nome ?? "seu treinador";

        await emailService.EnviarAsync(
            emailDestino,
            "Vínculo aprovado — forzion.tech",
            EmailTemplates.VinculoAprovado(aluno.Nome, nomeTreinador),
            cancellationToken).ConfigureAwait(false);
    }
}
