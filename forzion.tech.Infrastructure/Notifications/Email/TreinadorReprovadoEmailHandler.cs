using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class TreinadorReprovadoEmailHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    ILogger<TreinadorReprovadoEmailHandler> logger) : IDomainEventHandler<TreinadorReprovadoEvent>
{
    public async Task HandleAsync(TreinadorReprovadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        if (treinador is null)
        {
            logger.LogWarning("TreinadorReprovadoEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);

        if (conta is null)
        {
            logger.LogWarning("TreinadorReprovadoEmailHandler: conta {Id} não encontrada.", treinador.ContaId);
            return;
        }

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Atualização do seu cadastro — forzion.tech",
            EmailTemplates.TreinadorReprovado(treinador.Nome),
            cancellationToken).ConfigureAwait(false);
    }
}
