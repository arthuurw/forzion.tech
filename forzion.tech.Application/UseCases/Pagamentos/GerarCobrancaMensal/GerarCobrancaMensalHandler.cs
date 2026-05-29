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

        // Verificação antecipada da conta Stripe: evita criar Pagamento (e chamar Stripe)
        // se o treinador ainda não completou o onboarding — o erro é determinístico e não
        // vale a pena entrar na transação serializable para descobri-lo.
        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(assinatura.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto || string.IsNullOrEmpty(contaRecebimento.StripeConnectAccountId))
            throw new DomainException("Treinador sem conta Stripe configurada.");

        Pagamento pagamento;

        // ── Atomicidade G-PAY-1 ──────────────────────────────────────────────────────
        // Abordagem: Stripe ANTES do único commit (single-write).
        //
        // Problema original (dois commits):
        //   commit-1 persiste Pagamento Pendente sem StripePaymentIntentId
        //   → Stripe chamado
        //   → commit-2 persiste intent id
        //   Se commit-2 falha: row fica Pendente sem intent ("zumbi") ocupando o slot
        //   único e sem PaymentIntent cobrável.
        //
        // Solução: chamar Stripe dentro da transação serializable, antes do único commit.
        //   1. Serializable tx garante que apenas um caller por vez vê/cria Pendente.
        //   2. Pagamento.Criar() gera o Guid; Stripe recebe esse Guid como idempotency key
        //      (StripeService já usa "pagamento-{guid:N}" em PaymentIntentRequestOptions).
        //   3. DefinirDados* aplica intent id/client-secret no objeto em memória.
        //   4. Um único CommitAsync persiste Pagamento já com StripePaymentIntentId — sem
        //      janela entre commit-1 e commit-2.
        //
        // Falha no único commit:
        //   → Pagamento nunca chegou ao banco → próximo retry não encontra Pendente →
        //     recria Pagamento com NOVO Guid → Stripe recebe idempotency key diferente
        //     (novo Guid) → novo intent. O intent anterior fica órfão no Stripe e expira
        //     em ≤24h (comportamento aceitável: nenhum dinheiro capturado).
        //
        // Tradeoff vs. Stripe-antes-de-tudo (sem tx):
        //   Manter a serializable tx é necessário para impedir concorrência de dois callers
        //   que ambos não veem Pendente e tentam criar simultaneamente. O Guid + idempotency
        //   key Stripe protege apenas contra retry da *mesma* instância; a tx protege
        //   contra corrida entre instâncias distintas (ex.: job + endpoint).
        //
        // Zumbi legado (Pendente sem StripePaymentIntentId de corridas anteriores ao fix):
        //   Ainda detectado e marcado Falhou antes de criar novo — caminho preservado abaixo.
        // ────────────────────────────────────────────────────────────────────────────────

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
