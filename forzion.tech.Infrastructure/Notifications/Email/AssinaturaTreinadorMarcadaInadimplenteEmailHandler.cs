using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AssinaturaTreinadorMarcadaInadimplenteEmailHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<AssinaturaTreinadorMarcadaInadimplenteEmailHandler> logger)
    : IDomainEventHandler<AssinaturaTreinadorMarcadaInadimplenteEvent>
{
    public async Task HandleAsync(AssinaturaTreinadorMarcadaInadimplenteEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("AssinaturaTreinadorMarcadaInadimplenteEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("AssinaturaTreinadorMarcadaInadimplenteEmailHandler: conta {Id} do treinador não encontrada.", treinador.ContaId);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/treinador/plano";

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Acesso restrito — inadimplência no plano forzion.tech",
            EmailTemplates.PlanoInadimplente(treinador.Nome, domainEvent.TentativasFalhasConsecutivas, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
