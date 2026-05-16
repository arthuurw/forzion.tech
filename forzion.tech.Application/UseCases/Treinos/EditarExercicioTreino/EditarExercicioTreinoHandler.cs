using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.EditarExercicioTreino;

public class EditarExercicioTreinoHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<EditarExercicioTreinoHandler> logger)
{
    public virtual Task<Result<TreinoResponse>> HandleAsync(
        EditarExercicioTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinoResponse>> HandleAsyncCore(
        EditarExercicioTreinoCommand command,
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

        var exercicio = treino.Exercicios.FirstOrDefault(e => e.Id == command.TreinoExercicioId)
            ?? throw new TreinoNaoEncontradoException();

        exercicio.AtualizarSeries(command.Series
            .Select(s => (s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso))
            .ToList());

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercício {TreinoExercicioId} editado no treino {TreinoId}.",
            command.TreinoExercicioId, command.TreinoId);

        var nomesExercicio = await exercicioRepository
            .ObterNomesPorIdsAsync(treino.Exercicios.Select(e => e.ExercicioId), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino, nomesExercicio: nomesExercicio));
    }
}
