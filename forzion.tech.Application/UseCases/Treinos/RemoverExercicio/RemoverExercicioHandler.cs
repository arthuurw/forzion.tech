using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.RemoverExercicio;

public class RemoverExercicioHandler(
    ITreinoRepository treinoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
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
            .ExisteParaTreinoAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);

        Treino.ValidarMutabilidade(executado);

        try
        {
            treino.RemoverExercicio(command.TreinoExercicioId);
        }
        catch (DomainException ex)
        {
            return Result.Failure<TreinoResponse>(Error.Business(ex.Message));
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercício {TreinoExercicioId} removido do treino {TreinoId}.", command.TreinoExercicioId, command.TreinoId);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino));
    }
}
