using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.Services;

public sealed record CriarPagamentoComIntentParams<TPagamento>(
    Func<CancellationToken, Task> CriarIntent,
    Func<CancellationToken, Task<TPagamento?>> ObterPendente,
    Func<TPagamento, TPagamento?> VerificarIdempotencia,
    Func<Result<TPagamento>> CriarPagamento,
    Func<TPagamento, Result> AplicarIntent,
    Func<TPagamento, CancellationToken, Task> AdicionarAsync
) where TPagamento : class
{
    public required Func<TPagamento, DateTime, Result> MarcarFalhou { get; init; }
    public required Func<TPagamento, string?> ObterPaymentIntentId { get; init; }
}

public sealed class CriarPagamentoComIntentService(
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    IDatabaseErrorInspector databaseErrorInspector,
    IStripeService stripeService,
    TimeProvider timeProvider,
    ILogger<CriarPagamentoComIntentService> logger)
{
    private const int MaxTentativas = 3;

    public async Task<Result<TPagamento>> ExecutarAsync<TPagamento>(
        CriarPagamentoComIntentParams<TPagamento> p,
        CancellationToken ct = default)
        where TPagamento : class
    {
        var preexistente = await p.ObterPendente(ct).ConfigureAwait(false);
        if (preexistente is not null)
        {
            var reutilizavel = p.VerificarIdempotencia(preexistente);
            if (reutilizavel is not null)
                return Result.Success(reutilizavel);
        }

        await p.CriarIntent(ct).ConfigureAwait(false);

        var tentativa = 0;
        while (true)
        {
            tentativa++;
            try
            {
                var resultado = await ExecutarTransacaoAsync(p, ct).ConfigureAwait(false);
                if (resultado.IsFailure)
                    return Result.Failure<TPagamento>(resultado.Error!);

                var (pagamento, intentDescartado) = resultado.Value;
                await CancelarIntentDescartadoAsync(p, pagamento, intentDescartado, ct).ConfigureAwait(false);
                return Result.Success(pagamento);
            }
            catch (Exception ex) when (EhConflitoConcorrencia(ex) && tentativa < MaxTentativas)
            {
                logger.LogWarning(ex, "Conflito de concorrência ao criar pagamento {Tipo}, tentativa {Tentativa}/{Max}. Retentando.",
                    typeof(TPagamento).Name, tentativa, MaxTentativas);
                await Task.Delay(TimeSpan.FromMilliseconds(50 * tentativa), timeProvider, ct).ConfigureAwait(false);
            }
        }
    }

    private Task<Result<(TPagamento Pagamento, string? IntentDescartado)>> ExecutarTransacaoAsync<TPagamento>(
        CriarPagamentoComIntentParams<TPagamento> p,
        CancellationToken ct)
        where TPagamento : class =>
        transactionProvider.ExecuteInTransactionAsync(IsolationLevel.Serializable, async (tx, innerCt) =>
        {
            string? intentDescartado = null;
            var pendente = await p.ObterPendente(innerCt).ConfigureAwait(false);
            if (pendente is not null)
            {
                var reutilizavel = p.VerificarIdempotencia(pendente);
                if (reutilizavel is not null)
                {
                    await tx.CommitAsync(innerCt).ConfigureAwait(false);
                    return Result.Success((reutilizavel, (string?)null));
                }

                logger.LogWarning("Pagamento zumbi/obsoleto {Tipo} detectado. Marcando como Falhou antes de recriar.",
                    typeof(TPagamento).Name);

                intentDescartado = p.ObterPaymentIntentId(pendente);

                var marcarResult = p.MarcarFalhou(pendente, timeProvider.GetUtcNow().UtcDateTime);
                if (marcarResult.IsFailure)
                    return Result.Failure<(TPagamento, string?)>(marcarResult.Error!);
            }

            var criarResult = p.CriarPagamento();
            if (criarResult.IsFailure)
                return Result.Failure<(TPagamento, string?)>(criarResult.Error!);
            var pagamento = criarResult.Value;

            var intentResult = p.AplicarIntent(pagamento);
            if (intentResult.IsFailure)
                return Result.Failure<(TPagamento, string?)>(intentResult.Error!);

            await p.AdicionarAsync(pagamento, innerCt).ConfigureAwait(false);
            await unitOfWork.CommitAsync(innerCt).ConfigureAwait(false);
            await tx.CommitAsync(innerCt).ConfigureAwait(false);
            return Result.Success((pagamento, intentDescartado));
        }, ct);

    private async Task CancelarIntentDescartadoAsync<TPagamento>(
        CriarPagamentoComIntentParams<TPagamento> p,
        TPagamento pagamento,
        string? intentDescartado,
        CancellationToken ct)
        where TPagamento : class
    {
        if (intentDescartado is null || intentDescartado == p.ObterPaymentIntentId(pagamento))
            return;

        try
        {
            var outcome = await stripeService.CancelarPaymentIntentAsync(intentDescartado, ct).ConfigureAwait(false);
            switch (outcome)
            {
                case CancelarPaymentIntentResultado.JaCapturado:
                    logger.LogCritical(
                        "PaymentIntent {PaymentIntentId} de um pendente {Tipo} descartado já foi capturado. Reconciliação por refund necessária.",
                        intentDescartado, typeof(TPagamento).Name);
                    break;
                case CancelarPaymentIntentResultado.JaCancelado:
                    logger.LogDebug("PaymentIntent {PaymentIntentId} de um pendente {Tipo} descartado já estava cancelado.",
                        intentDescartado, typeof(TPagamento).Name);
                    break;
                default:
                    logger.LogInformation("PaymentIntent {PaymentIntentId} de um pendente {Tipo} descartado foi cancelado.",
                        intentDescartado, typeof(TPagamento).Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha transitória ao cancelar PaymentIntent {PaymentIntentId} de um pendente {Tipo} descartado.",
                intentDescartado, typeof(TPagamento).Name);
        }
    }

    private bool EhConflitoConcorrencia(Exception ex) =>
        databaseErrorInspector.EhViolacaoDeUnicidade(ex) || databaseErrorInspector.EhConflitoDeSerializacao(ex);
}
