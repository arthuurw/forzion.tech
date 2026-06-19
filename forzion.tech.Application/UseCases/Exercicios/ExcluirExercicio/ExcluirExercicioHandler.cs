using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;

public class ExcluirExercicioHandler(
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork)
{
    public virtual Task<Result> HandleAsync(
        ExcluirExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ExcluirExercicioCommand command,
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

        if (await exercicioRepository.EstaEmUsoAsync(command.ExercicioId, cancellationToken).ConfigureAwait(false))
            return Result.Failure(Error.Conflict("exercicio.em_uso", "Este exercício está em uso em fichas de treino e não pode ser excluído."));

        await exercicioRepository.RemoverAsync(exercicio, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
