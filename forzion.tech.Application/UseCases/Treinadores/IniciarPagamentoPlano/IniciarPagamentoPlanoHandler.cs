using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
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
    CriarPagamentoComIntentService criarPagamentoService,
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

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var params_ = new CriarPagamentoComIntentParams<PagamentoTreinador>(
            ObterPendente: ct => pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, ct),
            VerificarIdempotencia: pendente => pendente.StripePaymentIntentId is not null ? pendente : null,
            CriarPagamento: () => PagamentoTreinador.Criar(
                treinador.Id, assinatura.Id, assinatura.Valor,
                FinalidadePagamentoTreinador.Cadastro, now, command.Metodo),
            AplicarIntentPix: async (pag, ct) =>
            {
                var r = await stripeService.CriarPixPlataformaPaymentIntentAsync(assinatura.Valor, pag.Id, ct).ConfigureAwait(false);
                return pag.DefinirDadosPix(r.PaymentIntentId, r.QrCode, r.QrCodeUrl, r.Expiracao, now);
            },
            AplicarIntentCartao: async (pag, ct) =>
            {
                var r = await stripeService.CriarCartaoPlataformaPaymentIntentAsync(assinatura.Valor, pag.Id, ct).ConfigureAwait(false);
                return pag.DefinirDadosCartao(r.PaymentIntentId, r.ClientSecret, now);
            },
            AdicionarAsync: (pag, ct) => pagamentoRepository.AdicionarAsync(pag, ct),
            Metodo: command.Metodo
        )
        { MarcarFalhou = (pag, agora) => pag.MarcarFalhou(agora) };

        var result = await criarPagamentoService.ExecutarAsync(params_, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Result.Failure<IniciarPagamentoPlanoResponse>(result.Error!);

        logger.LogInformation("Pagamento {Metodo} do plano iniciado para treinador {TreinadorId}, pagamento {PagamentoId}.",
            command.Metodo, treinador.Id, result.Value.Id);

        return Result.Success(IniciarPagamentoPlanoResponse.De(result.Value));
    }
}
