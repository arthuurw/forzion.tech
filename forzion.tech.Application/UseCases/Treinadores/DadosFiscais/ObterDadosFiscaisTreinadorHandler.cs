using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.DadosFiscais;

public class ObterDadosFiscaisTreinadorHandler(ITreinadorRepository treinadorRepository)
{
    public virtual async Task<Result<DadosFiscaisResponse?>> HandleAsync(
        Guid treinadorId,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<DadosFiscaisResponse?>(TreinadorErrors.NaoEncontrado);

        return Result.Success(treinador.DadosFiscais is null
            ? null
            : DefinirDadosFiscaisTreinadorHandler.MapResponse(treinador.DadosFiscais));
    }
}
