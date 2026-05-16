using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class TreinadorAprovadoEmailHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    ILogger<TreinadorAprovadoEmailHandler> logger) : IDomainEventHandler<TreinadorAprovadoEvent>
{
    public async Task HandleAsync(TreinadorAprovadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        if (treinador is null)
        {
            logger.LogWarning("TreinadorAprovadoEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);

        if (conta is null)
        {
            logger.LogWarning("TreinadorAprovadoEmailHandler: conta {Id} não encontrada.", treinador.ContaId);
            return;
        }

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Sua conta foi aprovada — forzion.tech",
            EmailTemplates.TreinadorAprovado(treinador.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
