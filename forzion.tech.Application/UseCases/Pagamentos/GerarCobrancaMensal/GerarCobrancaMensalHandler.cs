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
    IAssinaturaAlunoRepository assinaturaRepository,
    IPagamentoRepository pagamentoRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    IOptions<PaymentSettings> paymentSettings,
    TimeProvider timeProvider,
    ILogger<GerarCobrancaMensalHandler> logger)
{
    private readonly decimal _taxaPlataformaPercent = paymentSettings.Value.TaxaPlataformaPercent;

    public virtual async Task<Result<PagamentoResponse>> HandleAsync(
        GerarCobrancaMensalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assinatura = await assinaturaRepository.ObterPorIdAsync(command.AssinaturaAlunoId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("AssinaturaAluno não encontrada.");

        if (assinatura.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        if (assinatura.Status == AssinaturaAlunoStatus.Cancelada)
            return Result.Failure<PagamentoResponse>(Error.Business("AssinaturaAluno cancelada não pode ser cobrada."));

        var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);
        if (pendente is not null)
        {
            if (pendente.StripePaymentIntentId is not null)
                return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(pendente));

            logger.LogWarning("Pagamento zumbi {PagamentoId} detectado para assinatura {AssinaturaAlunoId}. Marcando como Falhou.", pendente.Id, assinatura.Id);
            pendente.MarcarFalhou();
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(assinatura.TreinadorId, cancellationToken).ConfigureAwait(false);

        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto || string.IsNullOrEmpty(contaRecebimento.StripeConnectAccountId))
            throw new DomainException("Treinador sem conta Stripe configurada.");

        var pagamento = Pagamento.Criar(assinatura.Id, assinatura.Valor, timeProvider.GetUtcNow().UtcDateTime, command.Metodo);
        await pagamentoRepository.AdicionarAsync(pagamento, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (command.Metodo == MetodoPagamento.Cartao)
            {
                var cartaoResult = await stripeService.CriarCartaoPaymentIntentAsync(
                    assinatura.Valor,
                    contaRecebimento.StripeConnectAccountId,
                    pagamento.Id,
                    _taxaPlataformaPercent,
                    cancellationToken).ConfigureAwait(false);

                pagamento.DefinirDadosCartao(cartaoResult.PaymentIntentId, cartaoResult.ClientSecret);
            }
            else
            {
                var pixResult = await stripeService.CriarPixPaymentIntentAsync(
                    assinatura.Valor,
                    contaRecebimento.StripeConnectAccountId,
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

        logger.LogInformation("Cobrança {Metodo} gerada para assinatura {AssinaturaAlunoId}, pagamento {PagamentoId}.",
            command.Metodo, assinatura.Id, pagamento.Id);

        return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(pagamento));
    }
}
