using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class DigestTreinadorEmailNotifier(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<DigestTreinadorEmailNotifier> logger) : IDigestTreinadorEmailNotifier
{
    public async Task NotificarAsync(Guid treinadorId, int treinaram, int naoTreinaram, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("DigestTreinadorEmailNotifier: treinador {Id} não encontrado — ignorado.", treinadorId);
            return;
        }

        var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("DigestTreinadorEmailNotifier: conta {Id} do treinador não encontrada — ignorado.", treinador.ContaId);
            return;
        }

        var canais = await planoNotificationPolicy.ResolverPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (!canais.Email) return;

        if (conta.NotificacoesEngajamentoEmailOptOut) return;

        await emailService.EnviarAsync(
            conta.Email.Value,
            "Resumo de aderência do dia — forzion.tech",
            EmailTemplates.DigestTreinador(treinador.Nome, treinaram, naoTreinaram),
            cancellationToken).ConfigureAwait(false);
    }
}
