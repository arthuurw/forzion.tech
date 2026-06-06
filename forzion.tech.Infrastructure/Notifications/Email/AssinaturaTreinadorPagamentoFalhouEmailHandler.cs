using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AssinaturaTreinadorPagamentoFalhouEmailHandler(
    IAssinaturaTreinadorRepository assinaturaRepository,
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<AssinaturaTreinadorPagamentoFalhouEmailHandler> logger)
    : IDomainEventHandler<AssinaturaTreinadorPagamentoFalhouEvent>
{
    public async Task HandleAsync(AssinaturaTreinadorPagamentoFalhouEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("AssinaturaTreinadorPagamentoFalhouEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("AssinaturaTreinadorPagamentoFalhouEmailHandler: conta {Id} do treinador não encontrada.", treinador.ContaId);
            return;
        }

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaTreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("AssinaturaTreinadorPagamentoFalhouEmailHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaTreinadorId);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/treinador/plano";

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Cobrança do plano não processada — forzion.tech",
            EmailTemplates.CobrancaPlanoFalhou(treinador.Nome, assinatura.Valor, domainEvent.TentativasFalhasConsecutivas, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
