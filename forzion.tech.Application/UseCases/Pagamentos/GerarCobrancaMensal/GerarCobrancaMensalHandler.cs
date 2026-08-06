using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
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
    CriarPagamentoComIntentService criarPagamentoService,
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

        var assinatura = await assinaturaRepository.ObterPorIdAsync(command.AssinaturaAlunoId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null)
            return Result.Failure<PagamentoResponse>(AssinaturaAlunoErrors.NaoEncontrada);

        if (assinatura.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        if (assinatura.Status == AssinaturaAlunoStatus.Cancelada)
            return Result.Failure<PagamentoResponse>(AssinaturaAlunoErrors.CanceladaNaoCobravel);

        // Verificação antecipada da conta Stripe: evita entrar na transação serializable
        // para descobrir um erro determinístico.
        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(assinatura.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto || string.IsNullOrEmpty(contaRecebimento.StripeConnectAccountId))
            return Result.Failure<PagamentoResponse>(Error.Business("treinador.sem_conta_stripe", "Treinador sem conta Stripe configurada."));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accountId = contaRecebimento.StripeConnectAccountId;
        var taxa = _taxaPlataformaPercent;
        var chave = IdempotencyKey.Cobranca("aluno", assinatura.Id, now);

        var pendenteAtual = await pagamentoRepository.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);
        if (pendenteAtual is not null && pendenteAtual.EstaVencido(now))
        {
            // Único ponto que percebe abandono de cartão: Stripe não emite webhook terminal pra PI nunca confirmado.
            assinatura.RegistrarPagamentoFalho(now);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        PixPaymentResult? pix = null;
        CartaoPaymentResult? cartao = null;

        var params_ = new CriarPagamentoComIntentParams<Pagamento>(
            CriarIntent: async ct =>
            {
                if (command.Metodo == MetodoPagamento.Cartao)
                    cartao = await stripeService.CriarCartaoPaymentIntentAsync(assinatura.Valor, accountId, taxa, chave, ct).ConfigureAwait(false);
                else
                    pix = await stripeService.CriarPixPaymentIntentAsync(assinatura.Valor, accountId, taxa, chave, ct).ConfigureAwait(false);
            },
            ObterPendente: ct => pagamentoRepository.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, ct),
            VerificarIdempotencia: pendente => pendente.StripePaymentIntentId is not null && !pendente.EstaVencido(now) ? pendente : null,
            CriarPagamento: () => Pagamento.Criar(assinatura.Id, assinatura.Valor, now, command.Metodo),
            AplicarIntent: pag => command.Metodo == MetodoPagamento.Cartao
                ? pag.DefinirDadosCartao(cartao!.PaymentIntentId, cartao.ClientSecret, now)
                : pag.DefinirDadosPix(pix!.PaymentIntentId, pix.QrCode, pix.QrCodeUrl, pix.Expiracao, now),
            AdicionarAsync: (pag, ct) => pagamentoRepository.AdicionarAsync(pag, ct)
        )
        { ObterPaymentIntentId = pag => pag.StripePaymentIntentId, MarcarFalhou = (pag, agora) => pag.MarcarFalhou(agora) };

        var result = await criarPagamentoService.ExecutarAsync(params_, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Result.Failure<PagamentoResponse>(result.Error!);

        logger.LogInformation("Cobrança {Metodo} gerada para assinatura {AssinaturaAlunoId}, pagamento {PagamentoId}.",
            command.Metodo, assinatura.Id, result.Value.Id);

        return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(result.Value));
    }
}
