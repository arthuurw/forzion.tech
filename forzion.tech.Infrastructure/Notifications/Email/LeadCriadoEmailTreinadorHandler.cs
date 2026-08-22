using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class LeadCriadoEmailTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    ILogger<LeadCriadoEmailTreinadorHandler> logger) : IDomainEventHandler<LeadCriadoEvent>
{
    public async Task HandleAsync(LeadCriadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("LeadCriadoEmailTreinadorHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);

        var emailDestino = conta?.Email.Value;
        if (emailDestino is null)
        {
            logger.LogWarning("LeadCriadoEmailTreinadorHandler: treinador {Id} sem e-mail — ignorado.", treinador.Id);
            return;
        }

        var origem = domainEvent.Source switch
        {
            LeadSource.Agent => "assistente virtual",
            LeadSource.Manual => "cadastro manual",
            _ => "canal desconhecido",
        };

        await emailService.EnviarAsync(
            emailDestino,
            "Novo lead — forzion.tech",
            EmailTemplates.NovoLead(treinador.Nome, origem),
            cancellationToken).ConfigureAwait(false);
    }
}
