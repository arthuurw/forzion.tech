using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.AtualizarTreino;

public class AtualizarTreinoHandler(
    ITreinoRepository treinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
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

        try
        {
            treino.Atualizar(command.Nome, command.Objetivo, command.Dificuldade, command.DataInicio, command.DataFim, command.LimparDataInicio, command.LimparDataFim);
        }
        catch (DomainException ex)
        {
            return Result.Failure<TreinoResponse>(Error.Business(ex.Message));
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treino {TreinoId} atualizado.", command.TreinoId);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino));
    }
}
