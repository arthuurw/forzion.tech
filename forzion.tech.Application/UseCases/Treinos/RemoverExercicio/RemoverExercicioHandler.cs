using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.RemoverExercicio;

public class RemoverExercicioHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    TimeProvider timeProvider,
    ILogger<RemoverExercicioHandler> logger)
{
    public virtual Task<Result<TreinoResponse>> HandleAsync(
        RemoverExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinoResponse>> HandleAsyncCore(
        RemoverExercicioCommand command,
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
        var removerResult = treino.RemoverExercicio(command.TreinoExercicioId, agora);
        if (removerResult.IsFailure)
            return Result.Failure<TreinoResponse>(removerResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercício {TreinoExercicioId} removido do treino {TreinoId}.", command.TreinoExercicioId, command.TreinoId);

        var nomesExercicio = await exercicioRepository
            .ObterNomesPorIdsAsync(treino.Exercicios.Select(e => e.ExercicioId), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino, nomesExercicio: nomesExercicio));
    }
}
