using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;

public class AdicionarExercicioHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IValidator<AdicionarExercicioCommand> validator,
    ILogger<AdicionarExercicioHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IExercicioRepository _exercicioRepository = exercicioRepository;
    private readonly IExecucaoTreinoRepository _execucaoTreinoRepository = execucaoTreinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<AdicionarExercicioCommand> _validator = validator;
    private readonly ILogger<AdicionarExercicioHandler> _logger = logger;

    public virtual async Task<TreinoResponse> HandleAsync(
        AdicionarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var treino = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (treino.TenantId != command.TenantId)
            throw new AcessoNegadoException();

        var executado = await _execucaoTreinoRepository
            .ExisteParaTreinoAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);

        if (executado)
            throw new TreinoExecutadoException();

        var exercicioExiste = await _exercicioRepository
            .ExisteAsync(command.ExercicioId, command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (!exercicioExiste)
            throw new ExercicioNaoEncontradoException();

        treino.AdicionarExercicio(command.ExercicioId, command.Series, command.Repeticoes, command.Carga, command.Descanso);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exercício {ExercicioId} adicionado ao treino {TreinoId}.", command.ExercicioId, command.TreinoId);

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
