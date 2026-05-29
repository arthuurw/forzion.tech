using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;

public class AdicionarExercicioHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IValidator<AdicionarExercicioCommand> validator,
    TimeProvider timeProvider,
    ILogger<AdicionarExercicioHandler> logger)
{
    public virtual Task<Result<TreinoResponse>> HandleAsync(
        AdicionarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinoResponse>> HandleAsyncCore(
        AdicionarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

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

        var exercicioExiste = await exercicioRepository
            .ExisteAsync(command.ExercicioId, treino.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        if (!exercicioExiste)
            throw new ExercicioNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var adicionarResult = treino.AdicionarExercicio(command.ExercicioId, agora);
        if (adicionarResult.IsFailure)
            return Result.Failure<TreinoResponse>(adicionarResult.Error!);
        var novoExercicio = adicionarResult.Value;

        foreach (var s in command.Series)
        {
            var serieResult = novoExercicio.AdicionarSerie(s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso);
            if (serieResult.IsFailure)
                return Result.Failure<TreinoResponse>(serieResult.Error!);
        }

        await treinoRepository.AdicionarTreinoExercicioAsync(novoExercicio, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercício {ExercicioId} adicionado ao treino {TreinoId}.", command.ExercicioId, command.TreinoId);

        var nomesExercicio = await exercicioRepository
            .ObterNomesPorIdsAsync(treino.Exercicios.Select(e => e.ExercicioId), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino, nomesExercicio: nomesExercicio));
    }
}
