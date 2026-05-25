using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Treinadores.ObterTreinador;

public class ObterTreinadorHandler(ITreinadorRepository treinadorRepository)
{
    public virtual async Task<TreinadorResponse> HandleAsync(
        Guid treinadorId,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository
            .ObterPorIdAsync(treinadorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        return new TreinadorResponse(
            treinador.Id,
            treinador.ContaId,
            treinador.Nome,
            treinador.Status,
            treinador.PlanoPlataformaId,
            treinador.CreatedAt);
    }
}
