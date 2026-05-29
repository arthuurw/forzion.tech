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

        await ProcessarEventoAsync(evento, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Núcleo de processamento de evento Stripe — invariante à origem (webhook live ou reconciliação
    /// via <c>Events.List</c>). Idempotente em todos os ramos via cheques de estado
    /// (<c>Pagamento.Status != Pendente</c>, <c>ContaRecebimento.OnboardingCompleto</c>).
    /// </summary>
    /// <remarks>
    /// Retorna o tipo de transição aplicada — útil pro reconciliador classificar
    /// entre <c>Replayed</c> (mudou estado), <c>JaConsistentes</c> (no-op por idempotência ou
    /// payload sem alvo na base) e <c>Ignorados</c> (tipo desconhecido).
    /// </remarks>
    public virtual async Task<ProcessarEventoResultado> ProcessarEventoAsync(
        StripeWebhookEvento evento,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        switch (evento.Type)
        {
            case "payment_intent.succeeded":
                return await ProcessarPagamentoPagoAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);

            case "payment_intent.payment_failed":
                return await ProcessarPagamentoFalhouAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);

            case "payment_intent.canceled":
                return await ProcessarPagamentoExpiradoAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);

            case "account.updated":
                return await ProcessarContaAtualizadaAsync(evento.AccountId!, evento.ChargesEnabled, cancellationToken).ConfigureAwait(false);

            default:
                logger.LogDebug("Evento Stripe ignorado: {EventType}.", evento.Type);
                return ProcessarEventoResultado.Ignorado;
        }
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

    private async Task<ProcessarEventoResultado> ProcessarPagamentoPagoAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (!await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;

        // Idempotência: Stripe entrega at-least-once; segundo disparo não deve retornar 500
        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
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
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarPagamentoFalhouAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return ProcessarEventoResultado.JaConsistente;

        if (!await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;

        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
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
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarPagamentoExpiradoAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return ProcessarEventoResultado.JaConsistente;

        if (!await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;

        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        pagamento.MarcarExpirado();
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Pagamento {PagamentoId} marcado como expirado.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarContaAtualizadaAsync(string accountId, bool chargesEnabled, CancellationToken ct)
    {
        if (!chargesEnabled) return ProcessarEventoResultado.JaConsistente;

        var contaRecebimento = await contaRecebimentoRepository.ObterPorStripeAccountIdAsync(accountId, ct).ConfigureAwait(false);
        if (contaRecebimento is null || contaRecebimento.OnboardingCompleto) return ProcessarEventoResultado.JaConsistente;

        contaRecebimento.ConfirmarOnboarding();
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Onboarding confirmado via webhook para treinador {TreinadorId}.", contaRecebimento.TreinadorId);
        return ProcessarEventoResultado.Aplicado;
    }
}

/// <summary>
/// Classificação do resultado de <see cref="ProcessarWebhookStripeHandler.ProcessarEventoAsync"/>.
/// Usado pelo reconciliador para contar replays vs. no-ops sem precisar inspecionar o estado
/// de cada pagamento.
/// </summary>
public enum ProcessarEventoResultado
{
    /// <summary>Mudou estado (Pagamento Pendente→Pago/Falhou/Expirado ou Onboarding confirmado).</summary>
    Aplicado,

    /// <summary>No-op: já estava no estado correto, alvo não existe, ou cross-account rejeitado.</summary>
    JaConsistente,

    /// <summary>Tipo de evento não tratado pela plataforma.</summary>
    Ignorado,
}
