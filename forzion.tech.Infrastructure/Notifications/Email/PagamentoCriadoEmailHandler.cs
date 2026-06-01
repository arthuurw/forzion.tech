using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// P0 (M6 follow-up) — notifica aluno via email que uma cobrança esta disponivel
/// no portal. Disparado por <see cref="PagamentoCriadoEvent"/> (cron de renovação
/// OU treinador-iniciado).
///
/// Resolução de destinatário: assinatura → aluno; preferência <c>Aluno.Email</c>,
/// fallback <c>Conta.Email</c> (mesmo pattern de AssinaturaAlunoCriadaEmailHandler).
/// Link sempre pro portal forzion (<c>App:FrontendBaseUrl</c>) — nunca Stripe.
/// </summary>
public sealed class PagamentoCriadoEmailHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<PagamentoCriadoEmailHandler> logger) : IDomainEventHandler<PagamentoCriadoEvent>
{
    public async Task HandleAsync(PagamentoCriadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoCriadoEmailHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(assinatura.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("PagamentoCriadoEmailHandler: aluno {Id} não encontrado.", assinatura.AlunoId);
            return;
        }

        string? emailDestino = aluno.Email?.Value;
        if (emailDestino is null)
        {
            var conta = await contaRepository.ObterPorIdAsync(aluno.ContaId, cancellationToken).ConfigureAwait(false);
            emailDestino = conta?.Email.Value;
        }

        if (emailDestino is null)
        {
            logger.LogWarning("PagamentoCriadoEmailHandler: aluno {Id} sem e-mail — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await emailService.EnviarAsync(
            emailDestino,
            "Cobrança disponível — forzion.tech",
            EmailTemplates.CobrancaDisponivel(aluno.Nome, domainEvent.Valor, domainEvent.MetodoPagamento, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
