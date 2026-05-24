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
                await ProcessarPagamentoPagoAsync(evento.PaymentIntentId!, cancellationToken).ConfigureAwait(false);
                break;

            case "payment_intent.payment_failed":
                await ProcessarPagamentoFalhouAsync(evento.PaymentIntentId!, cancellationToken).ConfigureAwait(false);
                break;

            case "payment_intent.canceled":
                await ProcessarPagamentoExpiradoAsync(evento.PaymentIntentId!, cancellationToken).ConfigureAwait(false);
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

    private async Task ProcessarPagamentoPagoAsync(string paymentIntentId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return;
        }

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
            assinatura.Ativar();
            assinatura.AgendarProximaCobranca(DateTime.UtcNow.AddMonths(1));
        }

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Pagamento {PagamentoId} marcado como pago.", pagamento.Id);
    }

    private async Task ProcessarPagamentoFalhouAsync(string paymentIntentId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return;

        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return;
        }

        pagamento.MarcarFalhou();
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Pagamento {PagamentoId} marcado como falhou.", pagamento.Id);
    }

    private async Task ProcessarPagamentoExpiradoAsync(string paymentIntentId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return;

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
