using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;

public class ExcluirExercicioHandler(
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork)
{
    public virtual async Task<Result> HandleAsync(
        ExcluirExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

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
            return Result.Failure(Error.Business("Este exercício está em uso em fichas de treino e não pode ser excluído."));

        await exercicioRepository.RemoverAsync(exercicio, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
