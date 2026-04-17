using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;

namespace forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;

public class ListarTreinosDoTreinadorHandler(ITreinoRepository treinoRepository)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;

    public virtual async Task<ListarTreinosResponse> HandleAsync(
        Guid treinadorId,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _treinoRepository
            .ListarPorTreinadorAsync(treinadorId, pagina, tamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        return new ListarTreinosResponse(
            items.Select(TreinoResponseExtensions.ToResponse).ToList(),
            total, pagina, tamanhoPagina);
    }
}
