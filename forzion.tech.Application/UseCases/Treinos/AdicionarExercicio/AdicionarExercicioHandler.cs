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
    IUserContext userContext,
    IValidator<AdicionarExercicioCommand> validator,
    ILogger<AdicionarExercicioHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IExercicioRepository _exercicioRepository = exercicioRepository;
    private readonly IExecucaoTreinoRepository _execucaoTreinoRepository = execucaoTreinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
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

        // Validar autorização
        if (!_userContext.IsSystemAdmin && treino.TreinadorId != _userContext.PerfilId)
            throw new AcessoNegadoException();

        var executado = await _execucaoTreinoRepository
            .ExisteParaTreinoAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);

        if (executado)
            throw new TreinoExecutadoException();

        var exercicioExiste = await _exercicioRepository
            .ExisteAsync(command.ExercicioId, treino.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        if (!exercicioExiste)
            throw new ExercicioNaoEncontradoException();

        var novoExercicio = treino.AdicionarExercicio(command.ExercicioId);
        foreach (var s in command.Series)
            novoExercicio.AdicionarSerie(s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso);

        await _treinoRepository.AdicionarTreinoExercicioAsync(novoExercicio, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exercício {ExercicioId} adicionado ao treino {TreinoId}.", command.ExercicioId, command.TreinoId);

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
