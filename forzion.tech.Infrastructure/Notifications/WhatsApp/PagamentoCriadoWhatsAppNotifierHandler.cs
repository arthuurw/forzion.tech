using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// P0 (M6 follow-up) — notifica aluno via WhatsApp que uma cobrança está disponível
/// no portal. Disparado por <see cref="PagamentoCriadoEvent"/>.
///
/// Resolução: assinatura → aluno; usa <c>aluno.Telefone</c>. Sem telefone, no-op
/// (e-mail handler cobre fallback). NullWhatsAppNotifier absorve silenciosamente
/// quando integração não configurada.
/// Link sempre pro portal forzion (<c>App:FrontendBaseUrl</c>), nunca Stripe.
/// </summary>
public sealed class PagamentoCriadoWhatsAppNotifierHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    IOptions<AppSettings> appSettings,
    ILogger<PagamentoCriadoWhatsAppNotifierHandler> logger) : IDomainEventHandler<PagamentoCriadoEvent>
{
    public async Task HandleAsync(PagamentoCriadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoCriadoWhatsAppNotifierHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("PagamentoCriadoWhatsAppNotifierHandler: aluno {Id} não encontrado.", assinatura.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("PagamentoCriadoWhatsAppNotifierHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(assinatura.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await whatsAppNotifier
            .SendTemplateAsync(
                aluno.Telefone,
                WhatsAppTemplates.CobrancaDisponivel(aluno.Nome, domainEvent.Valor, domainEvent.MetodoPagamento, linkPortal),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
