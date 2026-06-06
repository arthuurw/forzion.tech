using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.Services;

public sealed record CriarPagamentoComIntentParams<TPagamento>(
    Func<CancellationToken, Task<TPagamento?>> ObterPendente,
    Func<TPagamento, TPagamento?> VerificarIdempotencia,
    Func<Result<TPagamento>> CriarPagamento,
    Func<TPagamento, CancellationToken, Task<Result>> AplicarIntentPix,
    Func<TPagamento, CancellationToken, Task<Result>> AplicarIntentCartao,
    Func<TPagamento, CancellationToken, Task> AdicionarAsync,
    MetodoPagamento Metodo
) where TPagamento : class
{
    public required Func<TPagamento, DateTime, Result> MarcarFalhou { get; init; }
}

public sealed class CriarPagamentoComIntentService(
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    TimeProvider timeProvider,
    ILogger<CriarPagamentoComIntentService> logger)
{
    public async Task<Result<TPagamento>> ExecutarAsync<TPagamento>(
        CriarPagamentoComIntentParams<TPagamento> p,
        CancellationToken ct = default)
        where TPagamento : class
    {
        TPagamento pagamento;

        await using (var tx = await transactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false))
        {
            var pendente = await p.ObterPendente(ct).ConfigureAwait(false);
            if (pendente is not null)
            {
                var reutilizavel = p.VerificarIdempotencia(pendente);
                if (reutilizavel is not null)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return Result.Success(reutilizavel);
                }

                logger.LogWarning("Pagamento zumbi/obsoleto {Tipo} detectado. Marcando como Falhou antes de recriar.",
                    typeof(TPagamento).Name);

                var marcarResult = p.MarcarFalhou(pendente, timeProvider.GetUtcNow().UtcDateTime);
                if (marcarResult.IsFailure)
                    return Result.Failure<TPagamento>(marcarResult.Error!);
            }

            var criarResult = p.CriarPagamento();
            if (criarResult.IsFailure)
                return Result.Failure<TPagamento>(criarResult.Error!);
            pagamento = criarResult.Value;

            var intentResult = p.Metodo == MetodoPagamento.Cartao
                ? await p.AplicarIntentCartao(pagamento, ct).ConfigureAwait(false)
                : await p.AplicarIntentPix(pagamento, ct).ConfigureAwait(false);

            if (intentResult.IsFailure)
                return Result.Failure<TPagamento>(intentResult.Error!);

            await p.AdicionarAsync(pagamento, ct).ConfigureAwait(false);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }

        return Result.Success(pagamento);
    }
}
