using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Treinadores.Agendamentos;

public class ListarSolicitacoesHandler(ISolicitacaoAgendamentoRepository solicitacaoAgendamentoRepository)
{
    public virtual Task<ListarSolicitacoesResponse> HandleAsync(
        ListarSolicitacoesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ListarSolicitacoesResponse> HandleAsyncCore(ListarSolicitacoesQuery query, CancellationToken cancellationToken)
    {
        var (items, total) = await solicitacaoAgendamentoRepository
            .ListarPorTreinadorAsync(query.TreinadorId, query.Status, query.Pagina, query.TamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        return new ListarSolicitacoesResponse(items, total, query.Pagina, query.TamanhoPagina);
    }
}
