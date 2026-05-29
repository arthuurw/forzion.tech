using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
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

            case "charge.refunded":
                return await ProcessarChargeReembolsadoAsync(evento.PaymentIntentId, evento.AmountRefundedCents, cancellationToken).ConfigureAwait(false);

            case "charge.dispute.created":
                return await ProcessarDisputaCriadaAsync(evento.PaymentIntentId, evento.MotivoDisputa, cancellationToken).ConfigureAwait(false);

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

        var agoraPago = timeProvider.GetUtcNow().UtcDateTime;
        var marcarPagoResult = pagamento.MarcarPago(agoraPago);
        if (marcarPagoResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} como pago: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarPagoResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        var assinatura = await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);
        if (assinatura is not null)
        {
            // Zera contador de falhas e reativa se estava Inadimplente.
            assinatura.RegistrarPagamentoRegularizado(agoraPago);
            // Só transiciona Pendente → Ativa via Ativar (Inadimplente já virou Ativa acima; Ativa permanece Ativa).
            if (assinatura.Status == AssinaturaAlunoStatus.Pendente)
            {
                var ativarResult = assinatura.Ativar(agoraPago);
                if (ativarResult.IsFailure)
                {
                    logger.LogWarning("Falha ao ativar assinatura {AssinaturaAlunoId} após pagamento {PaymentIntentId}: {Erro}. Tratando como não aplicado.",
                        assinatura.Id, paymentIntentId, ativarResult.Error!.Message);
                    return ProcessarEventoResultado.JaConsistente;
                }
            }
            var agendarResult = assinatura.AgendarProximaCobranca(agoraPago.AddMonths(1), agoraPago);
            if (agendarResult.IsFailure)
            {
                logger.LogWarning("Falha ao agendar próxima cobrança da assinatura {AssinaturaAlunoId} após pagamento {PaymentIntentId}: {Erro}. Tratando como não aplicado.",
                    assinatura.Id, paymentIntentId, agendarResult.Error!.Message);
                return ProcessarEventoResultado.JaConsistente;
            }
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

        var agoraFalhou = timeProvider.GetUtcNow().UtcDateTime;
        var marcarFalhouResult = pagamento.MarcarFalhou(agoraFalhou);
        if (marcarFalhouResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} como falhou: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarFalhouResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        // G-PAY-2: carrega assinatura ANTES do commit e muta ambos (pagamento + assinatura)
        // numa única transação. Evita dessincronismo se crash ocorrer entre os dois commits.
        // RegistrarPagamentoFalho pode disparar transição Ativa → Inadimplente (threshold).
        var assinatura = await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);
        assinatura?.RegistrarPagamentoFalho(agoraFalhou);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);

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

        var marcarExpiradoResult = pagamento.MarcarExpirado(timeProvider.GetUtcNow().UtcDateTime);
        if (marcarExpiradoResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} como expirado: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarExpiradoResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Pagamento {PagamentoId} marcado como expirado.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarChargeReembolsadoAsync(string? paymentIntentId, long? amountRefundedCents, CancellationToken ct)
    {
        // charge.refunded sem payment_intent — refund de charge avulso fora do nosso fluxo
        // (nunca cobramos sem PaymentIntent). Log e ignora.
        if (string.IsNullOrEmpty(paymentIntentId))
        {
            logger.LogWarning("charge.refunded recebido sem payment_intent. Ignorado.");
            return ProcessarEventoResultado.JaConsistente;
        }

        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("charge.refunded para PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        // Cross-account check não se aplica: refund parte do MESMO Connect account do treinador
        // (Stripe Dashboard dele), e não há vetor de replay útil — refund inverte dinheiro do
        // próprio destino, não há ganho pra atacante.

        // Idempotência: at-least-once. Se já Estornado, segunda entrega é no-op silencioso.
        // Pendente/Falhou/Expirado é estado inconsistente (refund de algo não-pago) — log warn + no-op.
        if (pagamento.Status == PagamentoStatus.Estornado)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já estornado. Ignorando re-entrega.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (pagamento.Status != PagamentoStatus.Pago)
        {
            logger.LogWarning(
                "charge.refunded para PaymentIntent {PaymentIntentId} em status inesperado {Status}. Ignorado.",
                paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        // G-PAY-5: só transiciona para Estornado em refund TOTAL.
        // Refund parcial é operação rara (ajuste manual do treinador); não há status parcial
        // no modelo — log + no-op é a opção de menor risco (não corrompe contabilidade).
        var valorPagamentoCents = (long)Math.Round(pagamento.Valor * 100m, MidpointRounding.AwayFromZero);
        if (amountRefundedCents.HasValue && amountRefundedCents.Value < valorPagamentoCents)
        {
            logger.LogInformation(
                "charge.refunded parcial para PaymentIntent {PaymentIntentId}: " +
                "refunded={RefundedCents} < total={TotalCents}. Status mantido como Pago.",
                paymentIntentId, amountRefundedCents.Value, valorPagamentoCents);
            return ProcessarEventoResultado.JaConsistente;
        }

        var marcarEstornadoResult = pagamento.MarcarEstornado(timeProvider.GetUtcNow().UtcDateTime);
        if (marcarEstornadoResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} como estornado: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarEstornadoResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Pagamento {PagamentoId} marcado como estornado (amountRefundedCents={AmountCents}).",
            pagamento.Id, amountRefundedCents);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarDisputaCriadaAsync(string? paymentIntentId, string? motivoDisputa, CancellationToken ct)
    {
        // charge.dispute.created sem payment_intent — disputa de charge avulso fora do nosso fluxo.
        if (string.IsNullOrEmpty(paymentIntentId))
        {
            logger.LogWarning("charge.dispute.created recebido sem payment_intent. Ignorado.");
            return ProcessarEventoResultado.JaConsistente;
        }

        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("charge.dispute.created para PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        // Cross-account check não se aplica: disputa parte do Connect account do treinador
        // (Stripe roteia evento direto). Sem vetor útil de replay cross-account aqui.

        // Idempotência: at-least-once. Se já EmDisputa, segunda entrega é no-op silencioso.
        if (pagamento.Status == PagamentoStatus.EmDisputa)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já em disputa. Ignorando re-entrega.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        // Disputa sobre estado diferente de Pago é incoerente (não há cobrança capturada
        // para disputar). Log warn + no-op — não lança DomainException para não derrubar
        // o pipeline de webhook em payload anômalo.
        if (pagamento.Status != PagamentoStatus.Pago)
        {
            logger.LogWarning(
                "charge.dispute.created para PaymentIntent {PaymentIntentId} em status inesperado {Status}. Ignorado.",
                paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        var agoraDisputa = timeProvider.GetUtcNow().UtcDateTime;
        var marcarDisputaResult = pagamento.MarcarEmDisputa(motivoDisputa ?? "unknown", agoraDisputa);
        if (marcarDisputaResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} em disputa: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarDisputaResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        // Força transição da assinatura Ativa → Inadimplente (drástico) — disputa é
        // sinal forte de fraude ou desistência; congela acesso já. RegistrarPagamentoFalho
        // (incrementa contador gradual) NÃO se aplica aqui.
        var assinatura = await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);
        if (assinatura is not null)
        {
            assinatura.MarcarInadimplentePorDisputa(agoraDisputa);
        }

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Pagamento {PagamentoId} marcado em disputa (motivo={Motivo}).",
            pagamento.Id, motivoDisputa ?? "unknown");
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarContaAtualizadaAsync(string accountId, bool chargesEnabled, CancellationToken ct)
    {
        if (!chargesEnabled) return ProcessarEventoResultado.JaConsistente;

        var contaRecebimento = await contaRecebimentoRepository.ObterPorStripeAccountIdAsync(accountId, ct).ConfigureAwait(false);
        if (contaRecebimento is null || contaRecebimento.OnboardingCompleto) return ProcessarEventoResultado.JaConsistente;

        var confirmarResult = contaRecebimento.ConfirmarOnboarding(timeProvider.GetUtcNow().UtcDateTime);
        if (confirmarResult.IsFailure)
        {
            logger.LogWarning("Falha ao confirmar onboarding do treinador {TreinadorId} via webhook: {Erro}. Tratando como não aplicado.",
                contaRecebimento.TreinadorId, confirmarResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }
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
