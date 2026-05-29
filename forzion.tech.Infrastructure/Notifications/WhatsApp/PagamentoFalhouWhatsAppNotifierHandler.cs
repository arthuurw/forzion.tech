using System.Globalization;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// IH.4 — notifica aluno via WhatsApp que uma cobrança falhou. Só dispara a
/// partir da 2ª tentativa consecutiva pra não spammar a 1ª falha (e-mail
/// cobre todas). Mensagem com tom progressivo.
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

        // Pula 1ª falha — e-mail já alerta. WhatsApp só a partir da escalada.
        if (domainEvent.TentativasFalhasConsecutivas < 2)
        {
            logger.LogDebug(
                "PagamentoFalhouWhatsAppNotifierHandler: tentativa {N} < 2 — ignorado.",
                domainEvent.TentativasFalhasConsecutivas);
            return;
        }

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
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var valorFormatado = assinatura.Valor.ToString("N2", ptBr);

        var aviso = domainEvent.TentativasFalhasConsecutivas switch
        {
            2 => "Segunda tentativa falhou. Atualize seu cartão antes da próxima.",
            _ => "Última tentativa antes do bloqueio. Regularize agora."
        };

        var mensagem =
            $"Olá, {aluno.Nome}! {aviso}\n" +
            $"Valor: R$ {valorFormatado}\n" +
            $"Tentativas: {domainEvent.TentativasFalhasConsecutivas}\n\n" +
            $"Acesse: {linkPortal}";

        await whatsAppNotifier
            .SendAsync(aluno.Telefone, mensagem, cancellationToken)
            .ConfigureAwait(false);
    }
}
