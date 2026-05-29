using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
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
    IDbContextTransactionProvider transactionProvider,
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

        Pagamento pagamento;

        // Wrap "select pendente → mark falhou → create novo" em transação serializable.
        // Sem isso, dois callers concorrentes podiam ambos não ver pendente, ambos
        // criarem pagamento novo — o índice parcial único pega o segundo, mas erra
        // tarde (com EF tracking polluído). Com a transação, o segundo bloqueia até
        // o primeiro commit/rollback e enxerga o estado consistente.
        await using (var tx = await transactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false))
        {
            var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);
            if (pendente is not null)
            {
                if (pendente.StripePaymentIntentId is not null)
                {
                    await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(pendente));
                }

                logger.LogWarning("Pagamento zumbi {PagamentoId} detectado para assinatura {AssinaturaAlunoId}. Marcando como Falhou.", pendente.Id, assinatura.Id);
                var marcarZumbiResult = pendente.MarcarFalhou(timeProvider.GetUtcNow().UtcDateTime);
                if (marcarZumbiResult.IsFailure)
                    // Sai do `await using` sem commit → rollback implícito da transação.
                    return Result.Failure<PagamentoResponse>(marcarZumbiResult.Error!);
            }

            var pagamentoResult = Pagamento.Criar(assinatura.Id, assinatura.Valor, timeProvider.GetUtcNow().UtcDateTime, command.Metodo);
            if (pagamentoResult.IsFailure)
                // Sai do `await using` sem commit → rollback implícito da transação.
                return Result.Failure<PagamentoResponse>(pagamentoResult.Error!);
            pagamento = pagamentoResult.Value;
            await pagamentoRepository.AdicionarAsync(pagamento, cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(assinatura.TreinadorId, cancellationToken).ConfigureAwait(false);

        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto || string.IsNullOrEmpty(contaRecebimento.StripeConnectAccountId))
            throw new DomainException("Treinador sem conta Stripe configurada.");

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

                var definirCartaoResult = pagamento.DefinirDadosCartao(cartaoResult.PaymentIntentId, cartaoResult.ClientSecret, timeProvider.GetUtcNow().UtcDateTime);
                if (definirCartaoResult.IsFailure)
                    return Result.Failure<PagamentoResponse>(definirCartaoResult.Error!);
            }
            else
            {
                var pixResult = await stripeService.CriarPixPaymentIntentAsync(
                    assinatura.Valor,
                    contaRecebimento.StripeConnectAccountId,
                    pagamento.Id,
                    _taxaPlataformaPercent,
                    cancellationToken).ConfigureAwait(false);

                var definirPixResult = pagamento.DefinirDadosPix(pixResult.PaymentIntentId, pixResult.QrCode, pixResult.QrCodeUrl, pixResult.Expiracao, timeProvider.GetUtcNow().UtcDateTime);
                if (definirPixResult.IsFailure)
                    return Result.Failure<PagamentoResponse>(definirPixResult.Error!);
            }
        }
        catch
        {
            pagamento.MarcarFalhou(timeProvider.GetUtcNow().UtcDateTime);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Cobrança {Metodo} gerada para assinatura {AssinaturaAlunoId}, pagamento {PagamentoId}.",
            command.Metodo, assinatura.Id, pagamento.Id);

        return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(pagamento));
    }
}
