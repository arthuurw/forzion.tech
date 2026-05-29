using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// IH.4 — notifica aluno via WhatsApp que uma cobrança falhou.
/// Dispara em TODAS as tentativas (parity com e-mail). Template carrega o
/// número de tentativas para progressividade.
///
/// Resolução: assinatura → aluno; usa <c>aluno.Telefone</c>. Sem telefone, no-op.
/// Link sempre pro portal forzion (<c>App:FrontendBaseUrl</c>), nunca Stripe.
/// </summary>
public sealed class PagamentoFalhouWhatsAppNotifierHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IOptions<AppSettings> appSettings,
    ILogger<PagamentoFalhouWhatsAppNotifierHandler> logger) : IDomainEventHandler<PagamentoFalhouEvent>
{
    public async Task HandleAsync(PagamentoFalhouEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoFalhouWhatsAppNotifierHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("PagamentoFalhouWhatsAppNotifierHandler: aluno {Id} não encontrado.", assinatura.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("PagamentoFalhouWhatsAppNotifierHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await whatsAppNotifier
            .SendTemplateAsync(
                aluno.Telefone,
                WhatsAppTemplates.CobrancaFalhou(aluno.Nome, assinatura.Valor, domainEvent.TentativasFalhasConsecutivas, linkPortal),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
