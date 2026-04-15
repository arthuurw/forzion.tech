using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.ListarAlunos;

public class ListarAlunosHandler(
    IAlunoRepository alunoRepository,
    ILogger<ListarAlunosHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly ILogger<ListarAlunosHandler> _logger = logger;

    public virtual async Task<ListarAlunosResponse> HandleAsync(
        ListarAlunosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, total) = await _alunoRepository
            .ListarAsync(query.TenantId, query.Pagina, query.TamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Listagem de alunos do tenant {TenantId}: {Total} registros.", query.TenantId, total);

        return new ListarAlunosResponse(
            items.Select(CadastrarAlunoHandler.ToResponse).ToList(),
            total,
            query.Pagina,
            query.TamanhoPagina
        );
    }
}
