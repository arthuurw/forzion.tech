using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;

namespace forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;

public class ListarAlunosAdminHandler(IAlunoRepository alunoRepository)
{
    public virtual Task<ListarAlunosResponse> HandleAsync(
        ListarAlunosAdminQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ListarAlunosResponse> HandleAsyncCore(
        ListarAlunosAdminQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await alunoRepository
            .ListarTodosAsync(query.Pagina, query.TamanhoPagina, query.Nome, query.Status, cancellationToken)
            .ConfigureAwait(false);

        return new ListarAlunosResponse(
            [.. items.Select(CadastrarAlunoHandler.ToResponse)],
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
