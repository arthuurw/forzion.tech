using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.DuplicarTreino;

public class DuplicarTreinoHandler(
    ITreinoRepository treinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<DuplicarTreinoHandler> logger)
{
    public virtual Task<TreinoResponse> HandleAsync(
        DuplicarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<TreinoResponse> HandleAsyncCore(
        DuplicarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        var original = await treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        // Validar autorização
        if (!userContext.IsSystemAdmin && original.TreinadorId != userContext.PerfilId)
            throw new AcessoNegadoException();

        var copia = original.Duplicar();

        await treinoRepository.AdicionarAsync(copia, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treino {TreinoId} duplicado como {CopiaTreinoId}.", command.TreinoId, copia.Id);

        return TreinoResponseExtensions.ToResponse(copia);
    }
}
