using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class NfseEmitidaEmailHandler(
    INotaFiscalRepository notaFiscalRepository,
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<NfseEmitidaEmailHandler> logger)
    : IDomainEventHandler<NotaFiscalEmitidaEvent>
{
    public async Task HandleAsync(NotaFiscalEmitidaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var nota = await notaFiscalRepository
            .ObterPorIdAsync(domainEvent.NotaFiscalId, cancellationToken)
            .ConfigureAwait(false);
        if (nota is null)
        {
            logger.LogWarning("NfseEmitidaEmailHandler: nota fiscal {Id} não encontrada.", domainEvent.NotaFiscalId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("NfseEmitidaEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("NfseEmitidaEmailHandler: conta {Id} do treinador não encontrada.", treinador.ContaId);
            return;
        }

        var numeroNfse = nota.NumeroNfse ?? nota.ChaveAcesso ?? domainEvent.ChaveAcesso;
        var dataEmissao = nota.DataEmissao ?? domainEvent.OcorridoEm;
        var linkNotas = $"{appSettings.Value.FrontendBaseUrl}/treinador/notas-fiscais";

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Nota fiscal emitida — forzion.tech",
            EmailTemplates.NfseEmitida(treinador.Nome, numeroNfse, nota.Valor, dataEmissao, linkNotas),
            cancellationToken).ConfigureAwait(false);
    }
}
