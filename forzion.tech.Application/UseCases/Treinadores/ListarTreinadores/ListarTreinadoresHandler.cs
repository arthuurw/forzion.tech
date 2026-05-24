using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.ListarTreinadores;

public record ListarTreinadoresResponse(
    IReadOnlyList<TreinadorResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);

public class ListarTreinadoresHandler(ITreinadorRepository treinadorRepository)
{
    public virtual async Task<ListarTreinadoresResponse> HandleAsync(
        TreinadorStatus? status, int pagina, int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await treinadorRepository
            .ListarAsync(status, pagina, tamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        var response = items.Select(t => new TreinadorResponse(
            t.Id, t.ContaId, t.Nome, t.Status, t.PlanoPlataformaId, t.CreatedAt)).ToList();

        return new ListarTreinadoresResponse(response, total, pagina, tamanhoPagina);
    }
}
