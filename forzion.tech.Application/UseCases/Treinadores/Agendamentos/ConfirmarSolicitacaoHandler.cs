using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.Agendamentos;

public class ConfirmarSolicitacaoHandler(
    ISolicitacaoAgendamentoRepository solicitacaoAgendamentoRepository,
    IPacoteRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    IDatabaseErrorInspector databaseErrorInspector,
    TimeProvider timeProvider,
    ILogger<ConfirmarSolicitacaoHandler> logger)
{
    private const int MaxTentativas = 3;

    public virtual Task<Result> HandleAsync(Guid treinadorId, Guid solicitacaoId, CancellationToken cancellationToken = default) =>
        HandleAsyncCore(treinadorId, solicitacaoId, cancellationToken);

    private async Task<Result> HandleAsyncCore(Guid treinadorId, Guid solicitacaoId, CancellationToken cancellationToken)
    {
        var tentativa = 0;
        while (true)
        {
            tentativa++;
            try
            {
                return await ExecutarTransacaoAsync(treinadorId, solicitacaoId, cancellationToken).ConfigureAwait(false);
            }
            // A NpgsqlExecutionStrategy reembrulha PostgresException — o inspector varre a cadeia
            // inteira de InnerException para reconhecer 40001 (R7/specification-concurrency §3).
            // EhConflitoDeConcorrenciaOtimista cobre o xmin de solicitacoes_agendamento:
            // Recusar/Cancelar commitando entre o SELECT e o UPDATE desta tx.
            catch (Exception ex) when ((databaseErrorInspector.EhConflitoDeSerializacao(ex) || databaseErrorInspector.EhConflitoDeConcorrenciaOtimista(ex)) && tentativa < MaxTentativas)
            {
                logger.LogWarning(ex, "Conflito de concorrência ao confirmar solicitação {SolicitacaoId}, tentativa {Tentativa}/{Max}. Retentando.",
                    solicitacaoId, tentativa, MaxTentativas);
                // A tx abortada não reverte o estado já mutado no ChangeTracker (EF não desfaz
                // property values no rollback) — sem isto, o retry relê a MESMA instância tracked
                // já em Confirmada pela tentativa que falhou, e Confirmar() rejeita a transição.
                unitOfWork.DescartarAlteracoesPendentes();
                await Task.Delay(TimeSpan.FromMilliseconds(50 * tentativa), timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private Task<Result> ExecutarTransacaoAsync(Guid treinadorId, Guid solicitacaoId, CancellationToken cancellationToken) =>
        transactionProvider.ExecuteInTransactionAsync(IsolationLevel.Serializable, async (tx, ct) =>
        {
            var solicitacao = await solicitacaoAgendamentoRepository
                .ObterPorIdAsync(solicitacaoId, treinadorId, ct)
                .ConfigureAwait(false);
            if (solicitacao is null)
                return Result.Failure(SolicitacaoAgendamentoErrors.NaoEncontrada);

            var pacote = await pacoteRepository.ObterPorIdAsync(solicitacao.PacoteId, ct).ConfigureAwait(false);
            if (pacote is null)
                return Result.Failure(PacoteErrors.NaoEncontrado);

            // Recontagem DENTRO da tx Serializable: sob Serializable, a leitura aqui e a escrita
            // de outra confirmação concorrente formam dependência read-write que o Postgres detecta
            // e aborta com 40001 em vez de deixar as duas commitarem (D-E).
            var confirmadas = await solicitacaoAgendamentoRepository
                .ContarConfirmadasSobrepostasAsync(treinadorId, solicitacao.InicioUtc, solicitacao.FimUtc, ct)
                .ConfigureAwait(false);
            if (confirmadas >= pacote.CapacidadeMaxima)
                return Result.Failure(SolicitacaoAgendamentoErrors.CapacidadeEsgotada);

            var confirmarResult = solicitacao.Confirmar(treinadorId, timeProvider.GetUtcNow().UtcDateTime);
            if (confirmarResult.IsFailure)
                return confirmarResult;

            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken);
}
