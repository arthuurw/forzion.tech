using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;

public record ExecucaoAlunoResponse(
    Guid ExecucaoId,
    Guid TreinoId,
    Guid AlunoId,
    DateTime DataExecucao,
    string? Observacao,
    DateTime CreatedAt,
    string NomeTreino,
    int TotalExercicios,
    int TotalSeries);

public record ListarExecucoesAlunoResponse(IReadOnlyList<ExecucaoAlunoResponse> Items, int Total, int Pagina, int TamanhoPagina);

public class ListarExecucoesAlunoHandler(IExecucaoTreinoRepository execucaoRepository, IUserContext userContext)
{
    public virtual async Task<ListarExecucoesAlunoResponse> HandleAsync(
        Guid alunoId,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        if (!userContext.IsSystemAdmin && userContext.PerfilId != alunoId)
            throw new AcessoNegadoException();

        var execucoes = await execucaoRepository.ListarComNomePorAlunoAsync(alunoId, pagina, tamanhoPagina, cancellationToken).ConfigureAwait(false);
        var total = await execucaoRepository.ContarPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        var items = execucoes
            .Select(e => new ExecucaoAlunoResponse(
                e.ExecucaoId, e.TreinoId, e.AlunoId, e.DataExecucao,
                e.Observacao, e.CreatedAt, e.NomeTreino, e.TotalExercicios, e.TotalSeries))
            .ToList();

        return new ListarExecucoesAlunoResponse(items, total, pagina, tamanhoPagina);
    }
}
