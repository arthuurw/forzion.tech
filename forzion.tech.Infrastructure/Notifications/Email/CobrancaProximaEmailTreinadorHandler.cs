using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class CobrancaProximaEmailTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<CobrancaProximaEmailTreinadorHandler> logger) : IDomainEventHandler<CobrancaProximaTreinadorEvent>
{
    public async Task HandleAsync(CobrancaProximaTreinadorEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("CobrancaProximaEmailTreinadorHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("CobrancaProximaEmailTreinadorHandler: conta {Id} do treinador não encontrada.", treinador.ContaId);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/treinador/plano";

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Seu plano renova em 3 dias — forzion.tech",
            EmailTemplates.CobrancaProxima(treinador.Nome, domainEvent.Valor, domainEvent.DataProximaCobranca, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
