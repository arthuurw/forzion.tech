using System.Globalization;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Outbox;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Services;

// Processa um lote de efeitos por ciclo. Lê sob lease (FOR UPDATE SKIP LOCKED), despacha
// cada um e grava o estado resultante na MESMA transação do lease — a mutação do handler
// durável e o avanço de status commitam juntos (atomicidade), soltando os locks no commit.
// Escopo por ciclo é responsabilidade do host (OutboxProcessorService).
public sealed class OutboxProcessor(
    AppDbContext context,
    IOutboxRepository repository,
    OutboxDispatcher dispatcher,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
{
    private readonly OutboxOptions _options = options.Value;

    public async Task<int> ProcessarLoteAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var idleMs = (int)_options.TimeoutTransacaoIdle.TotalMilliseconds;
        var sqlIdleTimeout = "SET LOCAL idle_in_transaction_session_timeout = " + idleMs.ToString(CultureInfo.InvariantCulture);
        await context.Database.ExecuteSqlRawAsync(sqlIdleTimeout, cancellationToken).ConfigureAwait(false);

        var itens = await repository.ObterProcessaveisAsync(_options.LotePorCiclo, agora, cancellationToken).ConfigureAwait(false);
        if (itens.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        foreach (var item in itens)
        {
            item.MarcarProcessando();
            try
            {
                await dispatcher.DespacharAsync(item, cancellationToken).ConfigureAwait(false);
                item.MarcarConcluido(timeProvider.GetUtcNow().UtcDateTime);
            }
            // Cancelamento de shutdown não é falha de efeito: re-lança p/ abortar o ciclo sem
            // gravar (rollback do lease → item segue Pendente), em vez de queimar uma tentativa.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RegistrarFalha(item, ex);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return itens.Count;
    }

    // Itens Concluido são histórico de auditoria de curta vida; passada a retenção, removidos
    // para a tabela não crescer indefinidamente. Pendente/Processando/Falhou são preservados
    // (Falhou é estado terminal auditável; ver relatório de saúde).
    public Task<int> LimparConcluidosAsync(CancellationToken cancellationToken = default)
    {
        var limite = timeProvider.GetUtcNow().UtcDateTime - _options.RetencaoConcluidos;
        return repository.LimparConcluidosAnterioresAsync(limite, cancellationToken);
    }

    private void RegistrarFalha(OutboxEfeito item, Exception ex)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (OutboxErroClassifier.EhPermanente(ex))
        {
            item.MarcarFalhouDefinitivo(ex.Message, agora);
            logger.LogCritical(ex,
                "Efeito outbox {Id} ({Tipo}) falhou com erro permanente; marcado Falhou sem retry.",
                item.Id, item.Tipo);
            return;
        }

        // item.Tentativas ainda é o valor ANTES desta tentativa; +1 = total após registrá-la.
        if (item.Tentativas + 1 >= _options.MaxTentativas)
        {
            item.MarcarFalhouDefinitivo(ex.Message, agora);
            logger.LogCritical(ex,
                "Efeito outbox {Id} ({Tipo}) falhou definitivamente após {Tentativas} tentativas.",
                item.Id, item.Tipo, item.Tentativas);
            return;
        }

        // Backoff exponencial: base · 2^tentativas-anteriores (ex.: 1min, 2min, 4min...).
        var atraso = item.Tentativas == 0
            ? _options.BackoffBase
            : _options.BackoffBase * Math.Pow(2, item.Tentativas);
        var proxima = agora.Add(atraso);
        item.RegistrarFalha(ex.Message, proxima);
        logger.LogWarning(ex,
            "Efeito outbox {Id} ({Tipo}) falhou; nova tentativa em {Proxima}.",
            item.Id, item.Tipo, proxima);
    }
}
