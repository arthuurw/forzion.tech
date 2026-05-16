using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;

namespace forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;

public class ListarTreinosDoTreinadorHandler(ITreinoRepository treinoRepository)
{
    public virtual async Task<ListarTreinosResponse> HandleAsync(
        Guid treinadorId,
        int pagina,
        int tamanhoPagina,
        string? nome = null,
        string? objetivo = null,
        string? ordenarPor = null,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await treinoRepository
            .ListarPorTreinadorAsync(treinadorId, pagina, tamanhoPagina, nome, objetivo, ordenarPor, cancellationToken)
            .ConfigureAwait(false);

        return new ListarTreinosResponse(
            items.Select(item => TreinoResponseExtensions.ToResponse(item.Treino, item.NomeAluno)).ToList(),
            total, pagina, tamanhoPagina);
    }
}
