using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;

public class AtualizarExercicioHandler(
    IExercicioRepository exercicioRepository,
    IGrupoMuscularRepository grupoMuscularRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<ExercicioResponse>> HandleAsync(
        AtualizarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<ExercicioResponse>> HandleAsyncCore(
        AtualizarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        var exercicio = await exercicioRepository.ObterPorIdAsync(command.ExercicioId, cancellationToken).ConfigureAwait(false)
            ?? throw new ExercicioNaoEncontradoException();

        if (command.TreinadorId is null)
        {
            if (!exercicio.IsGlobal)
                throw new AcessoNegadoException();
        }
        else
        {
            if (exercicio.TreinadorId != command.TreinadorId)
                throw new AcessoNegadoException();
        }

        if (command.Nome is not null
            && !string.Equals(command.Nome.Trim(), exercicio.Nome, StringComparison.OrdinalIgnoreCase)
            && await exercicioRepository.NomeJaExisteAsync(command.Nome, command.TreinadorId, command.ExercicioId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ExercicioResponse>(Error.Business("exercicio.nome_duplicado", "Já existe um exercício com este nome nesta biblioteca."));
        }

        if (command.GrupoMuscularId is not null
            && await grupoMuscularRepository.ObterPorIdAsync(command.GrupoMuscularId.Value, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new GrupoMuscularNaoEncontradoException();
        }

        var atualizarResult = exercicio.Atualizar(command.Nome, command.GrupoMuscularId, command.Descricao, timeProvider.GetUtcNow().UtcDateTime, command.ComoExecutar, command.VideoUrl);
        if (atualizarResult.IsFailure)
            return Result.Failure<ExercicioResponse>(atualizarResult.Error!);

        var grupoMuscular = await grupoMuscularRepository.ObterPorIdAsync(exercicio.GrupoMuscularId, cancellationToken).ConfigureAwait(false)
            ?? throw new GrupoMuscularNaoEncontradoException();

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ExercicioResponseExtensions.ToResponse(exercicio, grupoMuscular.Nome));
    }
}
