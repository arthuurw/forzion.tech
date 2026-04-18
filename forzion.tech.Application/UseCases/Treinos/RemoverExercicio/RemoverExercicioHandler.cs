using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.RemoverExercicio;

public class RemoverExercicioHandler(
    ITreinoRepository treinoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<RemoverExercicioHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IExecucaoTreinoRepository _execucaoTreinoRepository = execucaoTreinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<RemoverExercicioHandler> _logger = logger;

    public virtual async Task<TreinoResponse> HandleAsync(
        RemoverExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        treino.RemoverExercicio(command.TreinoExercicioId);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exercício {TreinoExercicioId} removido do treino {TreinoId}.", command.TreinoExercicioId, command.TreinoId);

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
