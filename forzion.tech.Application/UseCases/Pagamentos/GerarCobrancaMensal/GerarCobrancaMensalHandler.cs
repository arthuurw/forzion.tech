using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;

public class GerarCobrancaMensalHandler(
    IAssinaturaRepository assinaturaRepository,
    IPagamentoRepository pagamentoRepository,
    ITreinadorRepository treinadorRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    IOptions<PaymentSettings> paymentSettings,
    ILogger<GerarCobrancaMensalHandler> logger)
{
    private readonly decimal _taxaPlataformaPercent = paymentSettings.Value.TaxaPlataformaPercent;

    public virtual async Task<Result<PagamentoResponse>> HandleAsync(
        GerarCobrancaMensalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assinatura = await assinaturaRepository.ObterPorIdAsync(command.AssinaturaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Assinatura não encontrada.");

        if (assinatura.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        if (assinatura.Status == AssinaturaStatus.Cancelada)
            return Result.Failure<PagamentoResponse>(Error.Business("Assinatura cancelada não pode ser cobrada."));

        var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);
        if (pendente is not null)
        {
            if (pendente.StripePaymentIntentId is not null)
                return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(pendente));

            logger.LogWarning("Pagamento zumbi {PagamentoId} detectado para assinatura {AssinaturaId}. Marcando como Falhou.", pendente.Id, assinatura.Id);
            pendente.MarcarFalhou();
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var treinador = await treinadorRepository.ObterPorIdAsync(assinatura.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (!treinador.StripeOnboardingCompleto || string.IsNullOrEmpty(treinador.StripeConnectAccountId))
            throw new DomainException("Treinador sem conta Stripe configurada.");

        var pagamento = Pagamento.Criar(assinatura.Id, assinatura.Valor, command.Metodo);
        await pagamentoRepository.AdicionarAsync(pagamento, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (command.Metodo == MetodoPagamento.Cartao)
            {
                var cartaoResult = await stripeService.CriarCartaoPaymentIntentAsync(
                    assinatura.Valor,
                    treinador.StripeConnectAccountId,
                    pagamento.Id,
                    _taxaPlataformaPercent,
                    cancellationToken).ConfigureAwait(false);

                pagamento.DefinirDadosCartao(cartaoResult.PaymentIntentId, cartaoResult.ClientSecret);
            }
            else
            {
                var pixResult = await stripeService.CriarPixPaymentIntentAsync(
                    assinatura.Valor,
                    treinador.StripeConnectAccountId,
                    pagamento.Id,
                    _taxaPlataformaPercent,
                    cancellationToken).ConfigureAwait(false);

                pagamento.DefinirDadosPix(pixResult.PaymentIntentId, pixResult.QrCode, pixResult.QrCodeUrl, pixResult.Expiracao);
            }
        }
        catch
        {
            pagamento.MarcarFalhou();
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Cobrança {Metodo} gerada para assinatura {AssinaturaId}, pagamento {PagamentoId}.",
            command.Metodo, assinatura.Id, pagamento.Id);

        return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(pagamento));
    }
}
