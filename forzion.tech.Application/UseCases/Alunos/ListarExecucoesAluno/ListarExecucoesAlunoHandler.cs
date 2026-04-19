using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;

public record ExecucaoAlunoResponse(Guid ExecucaoId, Guid TreinoId, Guid AlunoId, DateTime DataExecucao, string? Observacao, DateTime CreatedAt);

public record ListarExecucoesAlunoResponse(IReadOnlyList<ExecucaoAlunoResponse> Items, int Total, int Pagina, int TamanhoPagina);

public class ListarExecucoesAlunoHandler(IExecucaoTreinoRepository execucaoRepository)
{
    private readonly IExecucaoTreinoRepository _execucaoRepository = execucaoRepository;

    public virtual async Task<ListarExecucoesAlunoResponse> HandleAsync(
        Guid alunoId,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var execucoes = await _execucaoRepository.ListarPorAlunoAsync(alunoId, pagina, tamanhoPagina, cancellationToken).ConfigureAwait(false);
        var total = await _execucaoRepository.ContarPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        var items = execucoes
            .Select(e => new ExecucaoAlunoResponse(e.Id, e.TreinoId, e.AlunoId, e.DataExecucao, e.Observacao, e.CreatedAt))
            .ToList();

        return new ListarExecucoesAlunoResponse(items, total, pagina, tamanhoPagina);
    }
}
