using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Treinadores.ListarTreinadoresPublicos;

public class ListarTreinadoresPublicosHandler(ITreinadorRepository treinadorRepository)
{
    public virtual async Task<IReadOnlyList<TreinadorResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var treinadores = await treinadorRepository
            .ListarAtivosAsync(cancellationToken)
            .ConfigureAwait(false);

        return treinadores
            .Select(t => new TreinadorResponse(
                t.Id,
                t.ContaId,
                t.Nome,
                t.Status,
                t.PlanoTreinadorId,
                t.CreatedAt))
            .ToList();
    }
}
