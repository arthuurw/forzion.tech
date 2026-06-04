using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.AtualizarTreino;

public class AtualizarTreinoHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    TimeProvider timeProvider,
    ILogger<AtualizarTreinoHandler> logger)
{
    public virtual Task<Result<TreinoResponse>> HandleAsync(
        AtualizarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinoResponse>> HandleAsyncCore(
        AtualizarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        var treino = await treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (!userContext.IsSystemAdmin && treino.TreinadorId != userContext.PerfilId)
            throw new AcessoNegadoException();

        var executado = await execucaoTreinoRepository
            .ExisteParaTreinoComAlunoAtivoAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);

        var mutabilidadeResult = Treino.ValidarMutabilidade(executado);
        if (mutabilidadeResult.IsFailure)
            return Result.Failure<TreinoResponse>(mutabilidadeResult.Error!);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var atualizarResult = treino.Atualizar(command.Nome, command.Objetivo, agora, command.Dificuldade, command.DataInicio, command.DataFim, command.LimparDataInicio, command.LimparDataFim);
        if (atualizarResult.IsFailure)
            return Result.Failure<TreinoResponse>(atualizarResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treino {TreinoId} atualizado.", command.TreinoId);

        var nomesExercicio = await exercicioRepository
            .ObterNomesPorIdsAsync(treino.Exercicios.Select(e => e.ExercicioId), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino, nomesExercicio: nomesExercicio));
    }
}
