using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.ListarTreinos;

public class ListarTreinosHandler(
    ITreinoRepository treinoRepository,
    ILogger<ListarTreinosHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly ILogger<ListarTreinosHandler> _logger = logger;

    public virtual async Task<ListarTreinosResponse> HandleAsync(
        ListarTreinosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, total) = await _treinoRepository
            .ListarPorAlunoAsync(query.AlunoId, query.Pagina, query.TamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Listagem de treinos do aluno {AlunoId}: {Total} registros.", query.AlunoId, total);

        return new ListarTreinosResponse(
            items.Select(t => TreinoResponseExtensions.ToResponse(t)).ToList(),
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
