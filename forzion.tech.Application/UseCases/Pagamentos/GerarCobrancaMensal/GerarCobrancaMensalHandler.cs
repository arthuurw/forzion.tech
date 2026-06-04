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

        var assinatura = await assinaturaRepository.ObterPorIdAsync(command.AssinaturaAlunoId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null)
            return Result.Failure<PagamentoResponse>(Error.NotFound("assinatura_aluno_nao_encontrada", "AssinaturaAluno não encontrada."));

        if (assinatura.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        if (assinatura.Status == AssinaturaAlunoStatus.Cancelada)
            return Result.Failure<PagamentoResponse>(Error.Business("AssinaturaAluno cancelada não pode ser cobrada."));

        // Verificação antecipada da conta Stripe: evita criar Pagamento (e chamar Stripe)
        // se o treinador ainda não completou o onboarding — o erro é determinístico e não
        // vale a pena entrar na transação serializable para descobri-lo.
        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(assinatura.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto || string.IsNullOrEmpty(contaRecebimento.StripeConnectAccountId))
            return Result.Failure<PagamentoResponse>(Error.Business("treinador_sem_conta_stripe", "Treinador sem conta Stripe configurada."));

        Pagamento pagamento;

        // Atomicidade G-PAY-1 (F12): Stripe ANTES do único commit (single-write).
        //   - Por quê não 2 commits: persistir Pendente, chamar Stripe, persistir o intent id
        //     num 2º commit deixa um Pendente sem intent ("zumbi") ocupando o slot único se o
        //     2º commit falha. Aqui o Guid de Pagamento.Criar() vira idempotency key do Stripe,
        //     DefinirDados* aplica o intent em memória e um único CommitAsync grava tudo junto.
        //   - Por quê serializable tx (e não só Stripe-antes-de-tudo): protege contra dois callers
        //     concorrentes (job + endpoint) que ambos não veem Pendente e criam em paralelo; o Guid
        //     só cobre retry da MESMA instância. Falha no commit → nada gravado → retry recria com
        //     novo Guid/intent; o intent órfão expira em ≤24h sem captura.
        //   - Zumbi legado (pré-fix): ainda detectado e marcado Falhou antes de recriar (abaixo).

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

                // Zumbi: Pendente sem intent id — criado antes deste fix ou por falha de
                // commit anterior. Marca Falhou para liberar o slot único e prosseguir.
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

            // Stripe chamado ANTES do commit. Se Stripe falhar: sai do await using sem
            // commit → rollback implícito → nenhum Pagamento gravado → sem zumbi.
            // O intent id é aplicado em memória; o commit único persiste tudo junto.
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

            await pagamentoRepository.AdicionarAsync(pagamento, cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Cobrança {Metodo} gerada para assinatura {AssinaturaAlunoId}, pagamento {PagamentoId}.",
            command.Metodo, assinatura.Id, pagamento.Id);

        return Result.Success(PagamentoResponseExtensions.ToResponseTreinador(pagamento));
    }
}
