using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.PerfilPublico;

public class ObterPerfilPublicoTreinadorHandler(ITreinadorRepository treinadorRepository)
{
    public virtual async Task<Result<PerfilPublicoResponse>> HandleAsync(
        Guid treinadorId,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<PerfilPublicoResponse>(TreinadorErrors.NaoEncontrado);

        return Result.Success(DefinirPerfilPublicoTreinadorHandler.MapResponse(treinador.PerfilPublico));
    }
}
