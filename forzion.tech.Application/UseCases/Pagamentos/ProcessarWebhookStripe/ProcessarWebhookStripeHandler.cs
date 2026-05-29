using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

public class ProcessarWebhookStripeHandler(
    IPagamentoRepository pagamentoRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ProcessarWebhookStripeHandler> logger)
{
    public virtual async Task<Result> HandleAsync(
        ProcessarWebhookStripeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var valido = await stripeService.ValidarWebhookAsync(command.Payload, command.AssinaturaAlunoStripe).ConfigureAwait(false);
        if (!valido)
            return Result.Failure(Error.Business("AssinaturaAluno do webhook inválida."));

        var evento = StripeWebhookParser.Parse(command.Payload);

        switch (evento.Type)
        {
            case "payment_intent.succeeded":
                await ProcessarPagamentoPagoAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);
                break;

            case "payment_intent.payment_failed":
                await ProcessarPagamentoFalhouAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);
                break;

            case "payment_intent.canceled":
                await ProcessarPagamentoExpiradoAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);
                break;

            case "account.updated":
                await ProcessarContaAtualizadaAsync(evento.AccountId!, evento.ChargesEnabled, cancellationToken).ConfigureAwait(false);
                break;

            default:
                logger.LogDebug("Evento Stripe ignorado: {EventType}.", evento.Type);
                break;
        }

        return Result.Success();
    }

    /// <summary>
    /// Defesa cross-account: se o evento Stripe traz `account`, valida que esse Connect
    /// account é exatamente o configurado para o treinador dono da assinatura. Sem isso,
    /// um payment_intent.* replicado de outro account assinado pelo mesmo webhook secret
    /// marcaria o pagamento errado como pago.
    /// </summary>
    private async Task<bool> ValidarConnectAccountAsync(Guid assinaturaAlunoId, string? eventAccountId, string paymentIntentId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(eventAccountId))
            return true; // evento direto (sem Connect routing) — nada a validar

        var assinatura = await assinaturaRepository.ObterPorIdAsync(assinaturaAlunoId, ct).ConfigureAwait(false);
        if (assinatura is null) return false;

        var conta = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(assinatura.TreinadorId, ct).ConfigureAwait(false);
        if (conta?.StripeConnectAccountId is null)
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} recebido com account {AccountId} mas treinador {TreinadorId} sem Connect account.",
                paymentIntentId, eventAccountId, assinatura.TreinadorId);
            return false;
        }

        if (!string.Equals(conta.StripeConnectAccountId, eventAccountId, StringComparison.Ordinal))
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} recebido com account {EventAccountId} ≠ Connect account do treinador {ExpectedAccountId}. Ignorado.",
                paymentIntentId, eventAccountId, conta.StripeConnectAccountId);
            return false;
        }

        return true;
    }

    private async Task ProcessarPagamentoPagoAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return;
        }

        if (!await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false))
            return;

        // Idempotência: Stripe entrega at-least-once; segundo disparo não deve retornar 500
        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return;
        }

        pagamento.MarcarPago();

        var assinatura = await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);
        if (assinatura is not null)
        {
            var agora = timeProvider.GetUtcNow().UtcDateTime;
            // Zera contador de falhas e reativa se estava Inadimplente.
            assinatura.RegistrarPagamentoRegularizado(agora);
            // Só transiciona Pendente → Ativa via Ativar (Inadimplente já virou Ativa acima; Ativa permanece Ativa).
            if (assinatura.Status == AssinaturaAlunoStatus.Pendente)
                assinatura.Ativar();
            assinatura.AgendarProximaCobranca(agora.AddMonths(1), agora);
        }

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Pagamento {PagamentoId} marcado como pago.", pagamento.Id);
    }

    private async Task ProcessarPagamentoFalhouAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return;

        if (!await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false))
            return;

        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return;
        }

        pagamento.MarcarFalhou();
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);

        // Carrega assinatura e incrementa contador de tentativas falhas.
        // Pode disparar transição Ativa → Inadimplente (RegistrarPagamentoFalho aplica o threshold).
        var assinatura = await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);
        if (assinatura is not null)
        {
            var agora = timeProvider.GetUtcNow().UtcDateTime;
            assinatura.RegistrarPagamentoFalho(agora);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }

        logger.LogInformation("Pagamento {PagamentoId} marcado como falhou.", pagamento.Id);
    }

    private async Task ProcessarPagamentoExpiradoAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return;

        if (!await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false))
            return;

        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return;
        }

        pagamento.MarcarExpirado();
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Pagamento {PagamentoId} marcado como expirado.", pagamento.Id);
    }

    private async Task ProcessarContaAtualizadaAsync(string accountId, bool chargesEnabled, CancellationToken ct)
    {
        if (!chargesEnabled) return;

        var contaRecebimento = await contaRecebimentoRepository.ObterPorStripeAccountIdAsync(accountId, ct).ConfigureAwait(false);
        if (contaRecebimento is null || contaRecebimento.OnboardingCompleto) return;

        contaRecebimento.ConfirmarOnboarding();
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Onboarding confirmado via webhook para treinador {TreinadorId}.", contaRecebimento.TreinadorId);
    }
}
