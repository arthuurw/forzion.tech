using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;

namespace forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;

public class ListarAlunosAdminHandler(IAlunoRepository alunoRepository)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;

    public virtual async Task<ListarAlunosResponse> HandleAsync(
        ListarAlunosAdminQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, total) = await _alunoRepository
            .ListarTodosAsync(query.Pagina, query.TamanhoPagina, query.Nome, query.Status, cancellationToken)
            .ConfigureAwait(false);

        return new ListarAlunosResponse(
            [.. items.Select(CadastrarAlunoHandler.ToResponse)],
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
