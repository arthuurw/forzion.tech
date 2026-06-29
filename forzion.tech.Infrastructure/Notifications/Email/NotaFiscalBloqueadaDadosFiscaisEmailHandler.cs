using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class NotaFiscalBloqueadaDadosFiscaisEmailHandler(
    INotaFiscalRepository notaFiscalRepository,
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<NotaFiscalBloqueadaDadosFiscaisEmailHandler> logger)
    : IDomainEventHandler<NotaFiscalBloqueadaDadosFiscaisEvent>
{
    public async Task HandleAsync(NotaFiscalBloqueadaDadosFiscaisEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var nota = await notaFiscalRepository
            .ObterPorIdAsync(domainEvent.NotaFiscalId, cancellationToken)
            .ConfigureAwait(false);
        if (nota is null)
        {
            logger.LogWarning("NotaFiscalBloqueadaDadosFiscaisEmailHandler: nota fiscal {Id} não encontrada.", domainEvent.NotaFiscalId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("NotaFiscalBloqueadaDadosFiscaisEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("NotaFiscalBloqueadaDadosFiscaisEmailHandler: conta {Id} do treinador não encontrada.", treinador.ContaId);
            return;
        }

        var linkDadosFiscais = $"{appSettings.Value.FrontendBaseUrl}/treinador/dados-fiscais";

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Ação necessária — complete seus dados fiscais",
            EmailTemplates.NfseBloqueadaDadosFiscais(treinador.Nome, linkDadosFiscais),
            cancellationToken).ConfigureAwait(false);
    }
}
