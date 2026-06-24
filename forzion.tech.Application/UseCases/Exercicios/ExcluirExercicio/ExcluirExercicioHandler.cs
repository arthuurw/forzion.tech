using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;

public class ExcluirExercicioHandler(
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork,
    ILogAprovacaoRepository logRepository,
    ILogger<ExcluirExercicioHandler> logger,
    TimeProvider timeProvider,
    IUserContext userContext)
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

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var logResult = await logRepository.RegistrarAsync(
            TipoAcaoAprovacao.ExclusaoExercicio,
            userContext.PerfilId,
            exercicio.Id,
            nameof(Exercicio),
            agora,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);

        await exercicioRepository.RemoverAsync(exercicio, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercicio {ExercicioId} excluído por {AtorId}.", exercicio.Id, userContext.PerfilId);

        return Result.Success();
    }
}
