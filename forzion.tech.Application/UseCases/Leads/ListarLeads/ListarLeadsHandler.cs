using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Leads.ListarLeads;

public class ListarLeadsHandler(ILeadRepository leadRepository)
{
    public virtual Task<ListarLeadsResponse> HandleAsync(
        ListarLeadsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ListarLeadsResponse> HandleAsyncCore(
        ListarLeadsQuery query,
        CancellationToken cancellationToken)
    {
        var (items, total) = await leadRepository.ListarAsync(
            query.TreinadorId,
            query.Pagina,
            query.TamanhoPagina,
            query.Status,
            query.Origem,
            query.Inicio,
            query.Fim,
            query.Termo,
            cancellationToken).ConfigureAwait(false);

        return new ListarLeadsResponse(items, total, query.Pagina, query.TamanhoPagina);
    }
}
