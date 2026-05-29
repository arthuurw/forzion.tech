using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica aluno via WhatsApp que uma cobrança paga foi estornada (refund manual
/// do treinador via Stripe Dashboard). Disparado por <see cref="PagamentoEstornadoEvent"/>.
///
/// Resolução: assinatura → aluno; usa <c>aluno.Telefone</c>. Sem telefone, no-op
/// (e-mail handler cobre fallback). NullWhatsAppNotifier absorve silenciosamente
/// quando integração não configurada.
/// Link sempre pro portal forzion (<c>App:FrontendBaseUrl</c>), nunca Stripe.
/// </summary>
public sealed class PagamentoEstornadoWhatsAppNotifierHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IOptions<AppSettings> appSettings,
    ILogger<PagamentoEstornadoWhatsAppNotifierHandler> logger) : IDomainEventHandler<PagamentoEstornadoEvent>
{
    public async Task HandleAsync(PagamentoEstornadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoEstornadoWhatsAppNotifierHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("PagamentoEstornadoWhatsAppNotifierHandler: aluno {Id} não encontrado.", assinatura.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("PagamentoEstornadoWhatsAppNotifierHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await whatsAppNotifier
            .SendTemplateAsync(
                aluno.Telefone,
                WhatsAppTemplates.PagamentoEstornado(aluno.Nome, domainEvent.Valor, linkPortal),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
