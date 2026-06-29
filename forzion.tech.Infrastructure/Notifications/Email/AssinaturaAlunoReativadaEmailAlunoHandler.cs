using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// notifica aluno via email que sua assinatura foi reativada após
/// regularização de pagamento (Inadimplente → Ativa).
/// Disparado por <see cref="AssinaturaAlunoReativadaEvent"/>.
///
/// Resolução de destinatário: aluno via <c>AlunoId</c>; preferência <c>Aluno.Email</c>,
/// fallback <c>Conta.Email</c>. Canal controlado por <see cref="IPlanoNotificationPolicy"/>
/// (resolver por treinador — quem paga o plano).
/// </summary>
public sealed class AssinaturaAlunoReativadaEmailAlunoHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    IOptions<AppSettings> appSettings,
    ILogger<AssinaturaAlunoReativadaEmailAlunoHandler> logger) : IDomainEventHandler<AssinaturaAlunoReativadaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoReativadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoReativadaEmailAlunoHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        string? emailDestino = aluno.Email?.Value;
        if (emailDestino is null)
        {
            var conta = await contaRepository
                .ObterPorIdAsync(aluno.ContaId, cancellationToken)
                .ConfigureAwait(false);
            emailDestino = conta?.Email.Value;
        }

        if (emailDestino is null)
        {
            logger.LogWarning("AssinaturaAlunoReativadaEmailAlunoHandler: aluno {Id} sem e-mail — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/assinatura";

        await emailService.EnviarAsync(
            emailDestino,
            "Assinatura reativada — forzion.tech",
            EmailTemplates.AssinaturaReativada(aluno.Nome, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
