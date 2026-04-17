using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.DuplicarTreino;

public class DuplicarTreinoHandler(
    ITreinoRepository treinoRepository,
    IUnitOfWork unitOfWork,
    ILogger<DuplicarTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<DuplicarTreinoHandler> _logger = logger;

    public virtual async Task<TreinoResponse> HandleAsync(
        DuplicarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var original = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        // TODO (Fase 5): validar autorização via IUserContext
        var copia = original.Duplicar();

        await _treinoRepository.AdicionarAsync(copia, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Treino {TreinoId} duplicado como {CopiaTreinoId}.", command.TreinoId, copia.Id);

        return TreinoResponseExtensions.ToResponse(copia);
    }
}
