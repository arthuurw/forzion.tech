using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;

public class AtualizarExercicioHandler(
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork)
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
            return Result.Failure<ExercicioResponse>(Error.Business("Já existe um exercício com este nome nesta biblioteca."));
        }

        exercicio.Atualizar(command.Nome, command.GrupoMuscular, command.Descricao);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ExercicioResponseExtensions.ToResponse(exercicio));
    }
}
