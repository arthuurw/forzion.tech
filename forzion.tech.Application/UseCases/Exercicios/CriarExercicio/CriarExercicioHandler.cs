using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public class CriarExercicioHandler(
    IExercicioRepository exercicioRepository,
    IGrupoMuscularRepository grupoMuscularRepository,
    IUnitOfWork unitOfWork,
    IValidator<CriarExercicioCommand> validator,
    TimeProvider timeProvider,
    ILogger<CriarExercicioHandler> logger)
{
    public virtual Task<Result<ExercicioResponse>> HandleAsync(
        CriarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<ExercicioResponse>> HandleAsyncCore(
        CriarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var grupoMuscular = await grupoMuscularRepository.ObterPorIdAsync(command.GrupoMuscularId, cancellationToken).ConfigureAwait(false)
            ?? throw new GrupoMuscularNaoEncontradoException();

        if (await exercicioRepository.NomeJaExisteAsync(command.Nome, command.TreinadorId, cancellationToken: cancellationToken).ConfigureAwait(false))
            return Result.Failure<ExercicioResponse>(Error.Conflict("exercicio.nome_duplicado", "Já existe um exercício com este nome nesta biblioteca."));

        var exercicioResult = Exercicio.Criar(command.Nome, command.GrupoMuscularId, timeProvider.GetUtcNow().UtcDateTime, command.TreinadorId, command.Descricao);
        if (exercicioResult.IsFailure)
            return Result.Failure<ExercicioResponse>(exercicioResult.Error!);
        var exercicio = exercicioResult.Value;

        await exercicioRepository.AdicionarAsync(exercicio, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercício {ExercicioId} criado.", exercicio.Id);

        return Result.Success(ExercicioResponseExtensions.ToResponse(exercicio, grupoMuscular.Nome));
    }
}
