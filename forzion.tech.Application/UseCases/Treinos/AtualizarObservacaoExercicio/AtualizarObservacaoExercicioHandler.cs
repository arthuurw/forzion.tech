using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.AtualizarObservacaoExercicio;

public class AtualizarObservacaoExercicioHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<AtualizarObservacaoExercicioHandler> logger)
{
    public virtual Task<TreinoResponse> HandleAsync(
        AtualizarObservacaoExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<TreinoResponse> HandleAsyncCore(
        AtualizarObservacaoExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        var treino = await treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (!userContext.IsSystemAdmin && treino.TreinadorId != userContext.PerfilId)
            throw new AcessoNegadoException();

        var exercicio = treino.Exercicios.FirstOrDefault(e => e.Id == command.TreinoExercicioId)
            ?? throw new TreinoNaoEncontradoException();

        var observacaoResult = exercicio.AtualizarObservacao(command.Observacao);
        if (observacaoResult.IsFailure)
            throw new DomainException(observacaoResult.Error!.Message);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Observação atualizada no exercício {ExercicioId} do treino {TreinoId}.",
            command.TreinoExercicioId, command.TreinoId);

        var nomesExercicio = await exercicioRepository
            .ObterNomesPorIdsAsync(treino.Exercicios.Select(e => e.ExercicioId), cancellationToken)
            .ConfigureAwait(false);

        return TreinoResponseExtensions.ToResponse(treino, nomesExercicio: nomesExercicio);
    }
}
