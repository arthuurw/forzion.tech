using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;

public class IniciarPagamentoPlanoHandler(
    ITreinadorRepository treinadorRepository,
    IAssinaturaTreinadorRepository assinaturaRepository,
    IPagamentoTreinadorRepository pagamentoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    TimeProvider timeProvider,
    ILogger<IniciarPagamentoPlanoHandler> logger)
{
    public virtual Task<Result<IniciarPagamentoPlanoResponse>> HandleAsync(
        IniciarPagamentoPlanoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<IniciarPagamentoPlanoResponse>> HandleAsyncCore(
        IniciarPagamentoPlanoCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            throw new TreinadorNaoEncontradoException();

        if (treinador.Status != TreinadorStatus.AguardandoPagamento)
            return Result.Failure<IniciarPagamentoPlanoResponse>(
                Error.Business("treinador_nao_aguardando_pagamento", "O treinador não está aguardando pagamento."));

        var assinatura = await assinaturaRepository.ObterAtualPorTreinadorAsync(treinador.Id, cancellationToken).ConfigureAwait(false);
        if (assinatura is null || assinatura.Status != AssinaturaTreinadorStatus.Pendente)
            return Result.Failure<IniciarPagamentoPlanoResponse>(
                Error.Business("assinatura_treinador_invalida", "Não há assinatura de treinador pendente para pagamento."));

        PagamentoTreinador pagamento;

        // Atomicidade (mesma estratégia do GerarCobrancaMensal/G-PAY-1): Stripe ANTES do único
        // commit (single-write). O Guid de PagamentoTreinador.Criar() vira a idempotency key do
        // Stripe; a tx serializable protege contra dois callers concorrentes criando em paralelo.
        await using (var tx = await transactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false))
        {
            var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);
            if (pendente is not null)
            {
                if (pendente.StripePaymentIntentId is not null)
                {
                    await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Success(IniciarPagamentoPlanoResponse.De(pendente));
                }

                // Zumbi: Pendente sem intent (falha de commit anterior) — marca Falhou para
                // liberar o slot único antes de recriar.
                logger.LogWarning("PagamentoTreinador zumbi {PagamentoId} detectado para assinatura {AssinaturaTreinadorId}. Marcando como Falhou.", pendente.Id, assinatura.Id);
                var marcarZumbiResult = pendente.MarcarFalhou(timeProvider.GetUtcNow().UtcDateTime);
                if (marcarZumbiResult.IsFailure)
                    return Result.Failure<IniciarPagamentoPlanoResponse>(marcarZumbiResult.Error!);
            }

            // Valor vem da assinatura — nunca do caller (endpoint é pré-login/AllowAnonymous).
            var pagamentoResult = PagamentoTreinador.Criar(
                treinador.Id, assinatura.Id, assinatura.Valor,
                FinalidadePagamentoTreinador.Cadastro, timeProvider.GetUtcNow().UtcDateTime, command.Metodo);
            if (pagamentoResult.IsFailure)
                return Result.Failure<IniciarPagamentoPlanoResponse>(pagamentoResult.Error!);
            pagamento = pagamentoResult.Value;

            if (command.Metodo == MetodoPagamento.Cartao)
            {
                var cartaoResult = await stripeService.CriarCartaoPlataformaPaymentIntentAsync(
                    assinatura.Valor, pagamento.Id, cancellationToken).ConfigureAwait(false);

                var definirCartaoResult = pagamento.DefinirDadosCartao(cartaoResult.PaymentIntentId, cartaoResult.ClientSecret, timeProvider.GetUtcNow().UtcDateTime);
                if (definirCartaoResult.IsFailure)
                    return Result.Failure<IniciarPagamentoPlanoResponse>(definirCartaoResult.Error!);
            }
            else
            {
                var pixResult = await stripeService.CriarPixPlataformaPaymentIntentAsync(
                    assinatura.Valor, pagamento.Id, cancellationToken).ConfigureAwait(false);

                var definirPixResult = pagamento.DefinirDadosPix(pixResult.PaymentIntentId, pixResult.QrCode, pixResult.QrCodeUrl, pixResult.Expiracao, timeProvider.GetUtcNow().UtcDateTime);
                if (definirPixResult.IsFailure)
                    return Result.Failure<IniciarPagamentoPlanoResponse>(definirPixResult.Error!);
            }

            await pagamentoRepository.AdicionarAsync(pagamento, cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Pagamento {Metodo} do plano iniciado para treinador {TreinadorId}, pagamento {PagamentoId}.",
            command.Metodo, treinador.Id, pagamento.Id);

        return Result.Success(IniciarPagamentoPlanoResponse.De(pagamento));
    }
}
