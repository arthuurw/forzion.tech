using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class TreinadorInativadoEmailHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    ILogger<TreinadorInativadoEmailHandler> logger) : IDomainEventHandler<TreinadorInativadoEvent>
{
    public async Task HandleAsync(TreinadorInativadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        if (treinador is null)
        {
            logger.LogWarning("TreinadorInativadoEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);

        if (conta is null)
        {
            logger.LogWarning("TreinadorInativadoEmailHandler: conta {Id} não encontrada.", treinador.ContaId);
            return;
        }

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Sua conta foi inativada — forzion.tech",
            EmailTemplates.TreinadorInativado(treinador.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
