using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.ListarAlunos;

public class ListarAlunosHandler(
    IAlunoRepository alunoRepository,
    ILogger<ListarAlunosHandler> logger)
{
    public virtual Task<ListarAlunosResponse> HandleAsync(
        ListarAlunosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ListarAlunosResponse> HandleAsyncCore(
        ListarAlunosQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await alunoRepository
            .ListarPorTreinadorAsync(query.TreinadorId, query.Pagina, query.TamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Listagem de alunos do treinador {TreinadorId}: {Total} registros.", query.TreinadorId, total);

        return new ListarAlunosResponse(
            [.. items.Select(CadastrarAlunoHandler.ToResponse)],
            total,
            query.Pagina,
            query.TamanhoPagina
        );
    }
}
