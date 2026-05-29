using System.Globalization;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Enums;
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
/// quando integração não configurada — handler não checa Habilitado.
/// Link sempre pro portal forzion (<c>App:FrontendBaseUrl</c>), nunca Stripe.
/// </summary>
public sealed class PagamentoCriadoWhatsAppNotifierHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IOptions<AppSettings> appSettings,
    ILogger<PagamentoCriadoWhatsAppNotifierHandler> logger) : IDomainEventHandler<PagamentoCriadoEvent>
{
    public async Task HandleAsync(PagamentoCriadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

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

        var metodoLabel = domainEvent.MetodoPagamento == MetodoPagamento.Cartao ? "cartão de crédito" : "Pix";
        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";
        // Formatação pt-BR explícita (R$ 149,90) — independe de culture do processo.
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var valorFormatado = domainEvent.Valor.ToString("N2", ptBr);

        var mensagem =
            $"Olá, {aluno.Nome}! Sua nova cobrança forzion.tech está disponível:\n" +
            $"Valor: R$ {valorFormatado}\n" +
            $"Método: {metodoLabel}\n\n" +
            $"Acesse: {linkPortal}";

        await whatsAppNotifier
            .SendAsync(aluno.Telefone, mensagem, cancellationToken)
            .ConfigureAwait(false);
    }
}
