using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

public class ProcessarWebhookStripeHandler(
    IPagamentoRepository pagamentoRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IPagamentoTreinadorRepository pagamentoTreinadorRepository,
    IAssinaturaTreinadorRepository assinaturaTreinadorRepository,
    ITreinadorRepository treinadorRepository,
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    IOutboxEnfileirador enfileirador,
    IDatabaseErrorInspector databaseErrorInspector,
    TimeProvider timeProvider,
    ILogger<ProcessarWebhookStripeHandler> logger)
{
    private const string TipoPlanoTreinador = "plano_treinador";

    private async Task<bool> CommitarTransicaoPagamentoAsync(string? paymentIntentId, CancellationToken ct)
    {
        try
        {
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (databaseErrorInspector.EhConflitoDeConcorrenciaOtimista(ex))
        {
            logger.LogDebug(ex,
                "Transição concorrente do PaymentIntent {PaymentIntentId} perdeu a corrida de xmin; entrega anterior já aplicou o efeito. Idempotente.",
                paymentIntentId);
            return false;
        }
    }

    public virtual async Task<Result> HandleAsync(
        ProcessarWebhookStripeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // age sobre o JSON do evento VERIFICADO retornado pela verificação, não sobre o
        // body raw não-confiável. null = assinatura inválida OU Livemode divergente.
        var eventoVerificadoJson = await stripeService.ValidarWebhookAsync(command.Payload, command.AssinaturaAlunoStripe).ConfigureAwait(false);
        if (eventoVerificadoJson is null)
            return Result.Failure(Error.Business("webhook_stripe.assinatura_invalida", "AssinaturaAluno do webhook inválida."));

        var evento = StripeWebhookParser.Parse(eventoVerificadoJson);

        await ProcessarEventoAsync(evento, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public virtual async Task<ProcessarEventoResultado> ProcessarEventoAsync(
        StripeWebhookEvento evento,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        switch (evento.Type)
        {
            case "payment_intent.succeeded":
                return evento.TipoMetadata == TipoPlanoTreinador
                    ? await ProcessarPagamentoTreinadorPagoAsync(evento.PaymentIntentId!, cancellationToken).ConfigureAwait(false)
                    : await ProcessarPagamentoPagoAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);

            case "payment_intent.payment_failed":
                return evento.TipoMetadata == TipoPlanoTreinador
                    ? await ProcessarPagamentoTreinadorFalhouAsync(evento.PaymentIntentId!, cancellationToken).ConfigureAwait(false)
                    : await ProcessarPagamentoFalhouAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);

            case "payment_intent.canceled":
                return evento.TipoMetadata == TipoPlanoTreinador
                    ? await ProcessarPagamentoTreinadorTransicaoAsync(evento.PaymentIntentId!, p => p.MarcarExpirado(timeProvider.GetUtcNow().UtcDateTime), cancellationToken).ConfigureAwait(false)
                    : await ProcessarPagamentoExpiradoAsync(evento.PaymentIntentId!, evento.AccountId, cancellationToken).ConfigureAwait(false);

            case "account.updated":
                return await ProcessarContaAtualizadaAsync(evento.AccountId!, evento.ChargesEnabled, cancellationToken).ConfigureAwait(false);

            case "charge.refunded":
                return await ProcessarChargeReembolsadoAsync(evento.PaymentIntentId, evento.AmountRefundedCents, cancellationToken).ConfigureAwait(false);

            case "charge.dispute.created":
                return await ProcessarDisputaCriadaAsync(evento.PaymentIntentId, evento.MotivoDisputa, evento.DisputeId, cancellationToken).ConfigureAwait(false);

            default:
                logger.LogDebug("Evento Stripe ignorado: {EventType}.", evento.Type);
                return ProcessarEventoResultado.Ignorado;
        }
    }

    // null StripeConnectAccountId = drift de configuração → lança para forçar 500 e retry do Stripe
    // (diferente de mismatch legítimo que retorna false/JaConsistente).
    // Retorna a AssinaturaAluno carregada para reutilização pelo caller (evita segundo round-trip).
    private async Task<(bool Valido, AssinaturaAluno? Assinatura)> ValidarConnectAccountAsync(
        Guid assinaturaAlunoId, string? eventAccountId, string paymentIntentId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(eventAccountId))
            return (true, null);

        var assinatura = await assinaturaRepository.ObterPorIdAsync(assinaturaAlunoId, ct).ConfigureAwait(false);
        if (assinatura is null) return (false, null);

        var conta = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(assinatura.TreinadorId, ct).ConfigureAwait(false);

        if (conta is not null && conta.StripeConnectAccountId is null)
        {
            // Conta existe mas sem Connect account: drift de dados (nunca deveria chegar aqui com account no evento).
            // Lança para forçar retry — não pode silenciar pois aluno pode ter pago e payment ficaria Pendente.
            throw new InvalidOperationException(
                $"PaymentIntent {paymentIntentId}: treinador {assinatura.TreinadorId} tem ContaRecebimento sem StripeConnectAccountId. Verificar onboarding.");
        }

        if (conta?.StripeConnectAccountId is null)
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} recebido com account {AccountId} mas treinador {TreinadorId} sem Connect account.",
                paymentIntentId, eventAccountId, assinatura.TreinadorId);
            return (false, null);
        }

        if (!string.Equals(conta.StripeConnectAccountId, eventAccountId, StringComparison.Ordinal))
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} recebido com account {EventAccountId} ≠ Connect account do treinador {ExpectedAccountId}. Ignorado.",
                paymentIntentId, eventAccountId, conta.StripeConnectAccountId);
            return (false, null);
        }

        return (true, assinatura);
    }

    private async Task<ProcessarEventoResultado> ProcessarPagamentoPagoAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        var (valido, assinaturaCarregada) = await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false);
        if (!valido)
            return ProcessarEventoResultado.JaConsistente;

        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        var agoraPago = timeProvider.GetUtcNow().UtcDateTime;

        // reutiliza assinatura já carregada em ValidarConnect (evita segundo round-trip).
        var assinatura = assinaturaCarregada
            ?? await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);

        if (assinatura is { Status: AssinaturaAlunoStatus.Cancelada })
            return await ReconciliarPixDeAssinaturaCanceladaAsync(pagamento, assinatura, paymentIntentId, agoraPago, ct).ConfigureAwait(false);

        var marcarPagoResult = pagamento.MarcarPago(agoraPago);
        if (marcarPagoResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} como pago: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarPagoResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (assinatura is not null)
        {
            assinatura.RegistrarPagamentoRegularizado(agoraPago);
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

        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;
        logger.LogInformation("Pagamento {PagamentoId} marcado como pago.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    // Refund precede MarcarEstornado/commit: falha do Stripe lança e aborta o commit (idempotência no
    // guard Status != Pendente do caller — redelivery vê Estornado e não reembolsa de novo).
    private async Task<ProcessarEventoResultado> ReconciliarPixDeAssinaturaCanceladaAsync(
        Pagamento pagamento, AssinaturaAluno assinatura, string paymentIntentId, DateTime agora, CancellationToken ct)
    {
        var marcarPagoResult = pagamento.MarcarPago(agora);
        if (marcarPagoResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} como pago antes do estorno automático: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarPagoResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        try
        {
            await stripeService.CriarReembolsoAsync(pagamento.Id, paymentIntentId, reverterTransferencia: true, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Falha ao reembolsar Pix capturado para assinatura cancelada {AssinaturaId} (PaymentIntent {PaymentIntentId}). Abortando commit para forçar retry.",
                assinatura.Id, paymentIntentId);
            throw new InvalidOperationException(
                $"Reembolso do Pix capturado falhou para assinatura {assinatura.Id} (PaymentIntent {paymentIntentId}). Retry necessário.", ex);
        }

        var marcarEstornadoResult = pagamento.MarcarEstornado(agora);
        if (marcarEstornadoResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} como estornado após reembolso: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarEstornadoResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;
        logger.LogWarning("Pix capturado para assinatura cancelada {AssinaturaId} — reembolsado automático (reverse transfer).", assinatura.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarPagamentoFalhouAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return ProcessarEventoResultado.JaConsistente;

        var (valido, assinaturaCarregada) = await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false);
        if (!valido)
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

        var assinatura = assinaturaCarregada
            ?? await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);
        assinatura?.RegistrarPagamentoFalho(agoraFalhou);

        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;

        logger.LogInformation("Pagamento {PagamentoId} marcado como falhou.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarPagamentoExpiradoAsync(string paymentIntentId, string? eventAccountId, CancellationToken ct)
    {
        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return ProcessarEventoResultado.JaConsistente;

        if (!string.IsNullOrEmpty(eventAccountId))
        {
            var (valido, _) = await ValidarConnectAccountAsync(pagamento.AssinaturaAlunoId, eventAccountId, paymentIntentId, ct).ConfigureAwait(false);
            if (!valido)
                return ProcessarEventoResultado.JaConsistente;
        }

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
        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;
        logger.LogInformation("Pagamento {PagamentoId} marcado como expirado.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarChargeReembolsadoAsync(string? paymentIntentId, long? amountRefundedCents, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(paymentIntentId))
        {
            logger.LogWarning("charge.refunded recebido sem payment_intent. Ignorado.");
            return ProcessarEventoResultado.JaConsistente;
        }

        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            var pagamentoTreinador = await pagamentoTreinadorRepository.ObterPorStripePaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
            if (pagamentoTreinador is not null)
                return await ProcessarEstornoTreinadorAsync(pagamentoTreinador, amountRefundedCents, ct).ConfigureAwait(false);

            logger.LogWarning("charge.refunded para PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

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

        if (!amountRefundedCents.HasValue)
            throw new InvalidOperationException($"charge.refunded para PaymentIntent {paymentIntentId} sem amount_refunded. Retry necessário.");

        // só transiciona para Estornado em refund total — parcial deixa em Pago.
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
        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;

        logger.LogInformation(
            "Pagamento {PagamentoId} marcado como estornado (amountRefundedCents={AmountCents}).",
            pagamento.Id, amountRefundedCents);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarEstornoTreinadorAsync(PagamentoTreinador pagamento, long? amountRefundedCents, CancellationToken ct)
    {
        if (pagamento.Status == PagamentoStatus.Estornado)
        {
            logger.LogDebug("PagamentoTreinador {PaymentIntentId} já estornado. Ignorando re-entrega.", pagamento.StripePaymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (pagamento.Status != PagamentoStatus.Pago)
        {
            logger.LogWarning("charge.refunded para PagamentoTreinador {PaymentIntentId} em status inesperado {Status}. Ignorado.",
                pagamento.StripePaymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (!amountRefundedCents.HasValue)
            throw new InvalidOperationException($"charge.refunded para PagamentoTreinador {pagamento.StripePaymentIntentId} sem amount_refunded. Retry necessário.");

        var valorPagamentoCents = (long)Math.Round(pagamento.Valor * 100m, MidpointRounding.AwayFromZero);
        if (amountRefundedCents.Value < valorPagamentoCents)
        {
            logger.LogInformation(
                "charge.refunded parcial para PagamentoTreinador {PaymentIntentId}: " +
                "refunded={RefundedCents} < total={TotalCents}. Status mantido como Pago.",
                pagamento.StripePaymentIntentId, amountRefundedCents.Value, valorPagamentoCents);
            return ProcessarEventoResultado.JaConsistente;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var result = pagamento.MarcarEstornado(agora);
        if (result.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PagamentoTreinador como estornado: {Erro}.", result.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        var assinatura = await assinaturaTreinadorRepository.ObterPorIdAsync(pagamento.AssinaturaTreinadorId, ct).ConfigureAwait(false);
        assinatura?.MarcarInadimplentePorDisputa(agora);

        if (!await CommitarTransicaoPagamentoAsync(pagamento.StripePaymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;
        logger.LogInformation("PagamentoTreinador {PagamentoId} marcado como estornado.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarDisputaCriadaAsync(string? paymentIntentId, string? motivoDisputa, string? disputeId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(paymentIntentId))
        {
            logger.LogWarning("charge.dispute.created recebido sem payment_intent. Ignorado.");
            return ProcessarEventoResultado.JaConsistente;
        }

        var pagamento = await pagamentoRepository.ObterPorPaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            var pagamentoTreinador = await pagamentoTreinadorRepository.ObterPorStripePaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
            if (pagamentoTreinador is not null)
                return await ProcessarDisputaTreinadorAsync(pagamentoTreinador, disputeId, ct).ConfigureAwait(false);

            logger.LogWarning("charge.dispute.created para PaymentIntent {PaymentIntentId} não encontrado.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (pagamento.Status == PagamentoStatus.EmDisputa)
        {
            // Linha fx: da 1ª disputa já garante entrega+retry via outbox; re-enfileirar
            // seria bloqueado pela chave única. Sem mutação de negócio.
            logger.LogDebug("PaymentIntent {PaymentIntentId} já em disputa. Outbox já enfileirado na 1ª entrega.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (pagamento.Status != PagamentoStatus.Pago)
        {
            logger.LogWarning(
                "charge.dispute.created para PaymentIntent {PaymentIntentId} em status inesperado {Status}. Ignorado.",
                paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (string.IsNullOrEmpty(disputeId))
            throw new InvalidOperationException($"charge.dispute.created para PaymentIntent {paymentIntentId} sem dispute id. Retry necessário.");

        var agoraDisputa = timeProvider.GetUtcNow().UtcDateTime;
        var marcarDisputaResult = pagamento.MarcarEmDisputa(motivoDisputa ?? "unknown", agoraDisputa);
        if (marcarDisputaResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PaymentIntent {PaymentIntentId} em disputa: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarDisputaResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        var assinatura = await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, ct).ConfigureAwait(false);
        if (assinatura is not null)
        {
            assinatura.MarcarInadimplentePorDisputa(agoraDisputa);
        }

        if (!string.IsNullOrEmpty(disputeId))
        {
            var payload = await DerivarPayloadEvidenciaAlunoAsync(disputeId, assinatura, pagamento, ct).ConfigureAwait(false);
            enfileirador.Enfileirar("fx:evidencia_disputa", payload, $"fx:evidencia_disputa:aluno:{pagamento.Id}");
        }

        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;

        logger.LogInformation(
            "Pagamento {PagamentoId} marcado em disputa (motivo={Motivo}).",
            pagamento.Id, motivoDisputa ?? "unknown");
        return ProcessarEventoResultado.Aplicado;
    }

    // DataUltimaAtividade é omitida: sem sinal real de uso barato, repetir DataUltimoPagamento
    // nos dois campos seria evidência falsa. Envia a data de pagamento uma única vez.
    private async Task<EvidenciaDisputaPayload> DerivarPayloadEvidenciaAlunoAsync(
        string disputeId, AssinaturaAluno? assinatura, Pagamento pagamento, CancellationToken ct)
    {
        string? email = null;
        if (assinatura is not null)
        {
            var aluno = await alunoRepository.ObterPorIdAsync(assinatura.AlunoId, ct).ConfigureAwait(false);
            if (aluno is not null)
            {
                var conta = await contaRepository.ObterPorIdAsync(aluno.ContaId, ct).ConfigureAwait(false);
                email = conta?.Email.Value ?? aluno.Email?.Value;
            }
        }

        return new EvidenciaDisputaPayload(disputeId, email, assinatura?.DataInicio, pagamento.DataPagamento, pagamento.Id);
    }

    private async Task<ProcessarEventoResultado> ProcessarDisputaTreinadorAsync(PagamentoTreinador pagamento, string? disputeId, CancellationToken ct)
    {
        if (pagamento.Status == PagamentoStatus.EmDisputa)
        {
            // Linha fx: da 1ª disputa já garante entrega+retry via outbox; re-enfileirar
            // seria bloqueado pela chave única. Sem mutação de negócio.
            logger.LogDebug("PagamentoTreinador {PaymentIntentId} já em disputa. Outbox já enfileirado na 1ª entrega.", pagamento.StripePaymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (pagamento.Status != PagamentoStatus.Pago)
        {
            logger.LogWarning("charge.dispute.created para PagamentoTreinador {PaymentIntentId} em status inesperado {Status}. Ignorado.",
                pagamento.StripePaymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        if (string.IsNullOrEmpty(disputeId))
            throw new InvalidOperationException($"charge.dispute.created para PagamentoTreinador {pagamento.StripePaymentIntentId} sem dispute id. Retry necessário.");

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var result = pagamento.MarcarEmDisputa(agora);
        if (result.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PagamentoTreinador em disputa: {Erro}.", result.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        var assinatura = await assinaturaTreinadorRepository.ObterPorIdAsync(pagamento.AssinaturaTreinadorId, ct).ConfigureAwait(false);
        assinatura?.MarcarInadimplentePorDisputa(agora);

        if (!string.IsNullOrEmpty(disputeId))
        {
            var payload = await DerivarPayloadEvidenciaTreinadorAsync(disputeId, assinatura, pagamento, ct).ConfigureAwait(false);
            enfileirador.Enfileirar("fx:evidencia_disputa", payload, $"fx:evidencia_disputa:treinador:{pagamento.Id}");
        }

        if (!await CommitarTransicaoPagamentoAsync(pagamento.StripePaymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;

        logger.LogInformation("PagamentoTreinador {PagamentoId} marcado em disputa.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<EvidenciaDisputaPayload> DerivarPayloadEvidenciaTreinadorAsync(
        string disputeId, AssinaturaTreinador? assinatura, PagamentoTreinador pagamento, CancellationToken ct)
    {
        string? email = null;
        var treinador = await treinadorRepository.ObterPorIdAsync(pagamento.TreinadorId, ct).ConfigureAwait(false);
        if (treinador is not null)
        {
            var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, ct).ConfigureAwait(false);
            email = conta?.Email.Value;
        }

        return new EvidenciaDisputaPayload(disputeId, email, assinatura?.DataInicio, pagamento.DataPagamento, pagamento.Id);
    }

    private async Task<ProcessarEventoResultado> ProcessarContaAtualizadaAsync(string accountId, bool chargesEnabled, CancellationToken ct)
    {
        var contaRecebimento = await contaRecebimentoRepository.ObterPorStripeAccountIdAsync(accountId, ct).ConfigureAwait(false);

        if (!chargesEnabled)
        {
            // chargesEnabled=false é normal durante onboarding; só é incidente se a conta JÁ estava operante.
            if (contaRecebimento?.OnboardingCompleto == true)
                logger.LogCritical(
                    "Conta Connect {AccountId} do treinador {TreinadorId} perdeu charges_enabled após onboarding concluído. Pagamentos do treinador estão bloqueados.",
                    accountId, contaRecebimento.TreinadorId);
            return ProcessarEventoResultado.JaConsistente;
        }

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

    private async Task<ProcessarEventoResultado> ProcessarPagamentoTreinadorPagoAsync(string paymentIntentId, CancellationToken ct)
    {
        var pagamento = await pagamentoTreinadorRepository.ObterPorStripePaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("PaymentIntent de plano de treinador {PaymentIntentId} não encontrado.", paymentIntentId);
            return ProcessarEventoResultado.JaConsistente;
        }
        if (pagamento.Status != PagamentoStatus.Pendente)
            return ProcessarEventoResultado.JaConsistente;

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        if (pagamento.MarcarPago(agora).IsFailure)
            return ProcessarEventoResultado.JaConsistente;

        if (pagamento.Finalidade == FinalidadePagamentoTreinador.Cadastro)
            await FinalizarCadastroAsync(pagamento, agora, ct).ConfigureAwait(false);

        if (pagamento.Finalidade == FinalidadePagamentoTreinador.Contratacao)
            await FinalizarContratacaoAsync(pagamento, agora, ct).ConfigureAwait(false);

        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;
        logger.LogInformation("PagamentoTreinador {PagamentoId} marcado como pago.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    // Lança InvalidOperationException em falha para impedir CommitAsync e forçar 500 → Stripe retenta.
    // Sem throw, mutação parcial (pagamento Pago, assinatura Pendente) ficaria persistida.
    private async Task FinalizarCadastroAsync(PagamentoTreinador pagamento, DateTime agora, CancellationToken ct)
    {
        var assinatura = await assinaturaTreinadorRepository.ObterPorIdAsync(pagamento.AssinaturaTreinadorId, ct).ConfigureAwait(false);
        var treinador = await treinadorRepository.ObterPorIdAsync(pagamento.TreinadorId, ct).ConfigureAwait(false);
        if (assinatura is null || treinador is null)
        {
            logger.LogWarning("Cadastro pago sem assinatura/treinador (pagamento {PagamentoId}).", pagamento.Id);
            throw new InvalidOperationException($"Assinatura ou treinador não encontrado para pagamento {pagamento.Id}. Retry necessário.");
        }

        var ativarResult = assinatura.Ativar(agora);
        if (ativarResult.IsFailure)
            throw new InvalidOperationException($"Falha ao ativar AssinaturaTreinador {assinatura.Id}: {ativarResult.Error!.Message}");

        assinatura.AgendarProximaCobranca(agora.AddMonths(1), agora);

        var confirmarResult = treinador.ConfirmarPagamentoPlano(agora);
        if (confirmarResult.IsFailure)
            throw new InvalidOperationException($"Falha ao confirmar pagamento do treinador {treinador.Id}: {confirmarResult.Error!.Message}");

        var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, ct).ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("Conta {ContaId} do treinador {TreinadorId} não encontrada — verificação não enviada.", treinador.ContaId, treinador.Id);
            throw new InvalidOperationException($"Conta {treinador.ContaId} não encontrada para treinador {treinador.Id}. Retry necessário.");
        }

        conta.EmitirRegistro(agora);
    }

    private async Task FinalizarContratacaoAsync(PagamentoTreinador pagamento, DateTime agora, CancellationToken ct)
    {
        var assinatura = await assinaturaTreinadorRepository.ObterPorIdAsync(pagamento.AssinaturaTreinadorId, ct).ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("Contratação paga sem assinatura (pagamento {PagamentoId}).", pagamento.Id);
            throw new InvalidOperationException($"Assinatura não encontrada para pagamento {pagamento.Id}. Retry necessário.");
        }

        var ativarResult = assinatura.Ativar(agora);
        if (ativarResult.IsFailure)
            throw new InvalidOperationException($"Falha ao ativar AssinaturaTreinador {assinatura.Id}: {ativarResult.Error!.Message}");

        assinatura.AgendarProximaCobranca(agora.AddMonths(1), agora);
    }

    private async Task<ProcessarEventoResultado> ProcessarPagamentoTreinadorFalhouAsync(string paymentIntentId, CancellationToken ct)
    {
        var pagamento = await pagamentoTreinadorRepository.ObterPorStripePaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null) return ProcessarEventoResultado.JaConsistente;

        if (pagamento.Status != PagamentoStatus.Pendente)
        {
            logger.LogDebug("PagamentoTreinador PaymentIntent {PaymentIntentId} já processado (status: {Status}). Ignorando re-entrega.", paymentIntentId, pagamento.Status);
            return ProcessarEventoResultado.JaConsistente;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var marcarFalhouResult = pagamento.MarcarFalhou(agora);
        if (marcarFalhouResult.IsFailure)
        {
            logger.LogWarning("Falha ao marcar PagamentoTreinador {PaymentIntentId} como falhou: {Erro}. Tratando como não aplicado.",
                paymentIntentId, marcarFalhouResult.Error!.Message);
            return ProcessarEventoResultado.JaConsistente;
        }

        var assinatura = await assinaturaTreinadorRepository.ObterPorIdAsync(pagamento.AssinaturaTreinadorId, ct).ConfigureAwait(false);
        assinatura?.RegistrarPagamentoFalho(agora);

        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;
        logger.LogInformation("PagamentoTreinador {PagamentoId} marcado como falhou.", pagamento.Id);
        return ProcessarEventoResultado.Aplicado;
    }

    private async Task<ProcessarEventoResultado> ProcessarPagamentoTreinadorTransicaoAsync(string paymentIntentId, Func<PagamentoTreinador, Result> transicao, CancellationToken ct)
    {
        var pagamento = await pagamentoTreinadorRepository.ObterPorStripePaymentIntentIdAsync(paymentIntentId, ct).ConfigureAwait(false);
        if (pagamento is null || pagamento.Status != PagamentoStatus.Pendente)
            return ProcessarEventoResultado.JaConsistente;

        if (transicao(pagamento).IsFailure)
            return ProcessarEventoResultado.JaConsistente;

        if (!await CommitarTransicaoPagamentoAsync(paymentIntentId, ct).ConfigureAwait(false))
            return ProcessarEventoResultado.JaConsistente;
        return ProcessarEventoResultado.Aplicado;
    }
}

public enum ProcessarEventoResultado
{
    Aplicado,
    JaConsistente,
    Ignorado,
}
