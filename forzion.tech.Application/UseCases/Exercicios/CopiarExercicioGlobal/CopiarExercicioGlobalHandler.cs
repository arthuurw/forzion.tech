using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.CopiarExercicioGlobal;

public class CopiarExercicioGlobalHandler(
    IExercicioRepository exercicioRepository,
    IGrupoMuscularRepository grupoMuscularRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CopiarExercicioGlobalHandler> logger)
{
    public virtual Task<Result<ExercicioResponse>> HandleAsync(
        CopiarExercicioGlobalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<ExercicioResponse>> HandleAsyncCore(
        CopiarExercicioGlobalCommand command,
        CancellationToken cancellationToken = default)
    {
        var original = await exercicioRepository.ObterPorIdAsync(command.ExercicioId, cancellationToken).ConfigureAwait(false)
            ?? throw new ExercicioNaoEncontradoException();

        if (!original.IsGlobal)
            throw new AcessoNegadoException();

        var copiaResult = Exercicio.Criar(original.Nome, original.GrupoMuscularId, timeProvider.GetUtcNow().UtcDateTime, command.TreinadorId, original.Descricao, original.ComoExecutar, original.VideoId);
        if (copiaResult.IsFailure)
            return Result.Failure<ExercicioResponse>(copiaResult.Error!);
        var copia = copiaResult.Value;

        await exercicioRepository.AdicionarAsync(copia, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercício global {OriginalId} copiado para treinador {TreinadorId} como {CopiaId}.",
            command.ExercicioId, command.TreinadorId, copia.Id);

        var grupoMuscular = await grupoMuscularRepository.ObterPorIdAsync(copia.GrupoMuscularId, cancellationToken).ConfigureAwait(false)
            ?? throw new GrupoMuscularNaoEncontradoException();

        return Result.Success(ExercicioResponseExtensions.ToResponse(copia, grupoMuscular.Nome));
    }
}
