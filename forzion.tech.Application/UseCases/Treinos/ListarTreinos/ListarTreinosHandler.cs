using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.ListarTreinos;

public class ListarTreinosHandler(
    ITreinoRepository treinoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext,
    ILogger<ListarTreinosHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<ListarTreinosHandler> _logger = logger;

    public virtual async Task<ListarTreinosResponse> HandleAsync(
        ListarTreinosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_userContext.IsSystemAdmin)
        {
            if (_userContext.IsAluno)
            {
                if (_userContext.PerfilId != query.AlunoId)
                    throw new AcessoNegadoException();
            }
            else
            {
                var vinculo = await _vinculoRepository
                    .ObterAtivoAsync(_userContext.PerfilId, query.AlunoId, cancellationToken)
                    .ConfigureAwait(false);

                if (vinculo is null)
                    throw new AcessoNegadoException();
            }
        }

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
