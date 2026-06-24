using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;

public class ListarPagamentosAssinaturaAlunoHandler(
    IPagamentoRepository pagamentoRepository,
    IAssinaturaAlunoRepository assinaturaRepository)
{
    public virtual async Task<ListarPagamentosAssinaturaAlunoResponse> HandleAsync(
        ListarPagamentosAssinaturaAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var assinatura = await assinaturaRepository.ObterPorIdAsync(query.AssinaturaAlunoId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null || assinatura.AlunoId != query.AlunoId)
            throw new AcessoNegadoException();

        var (pagamentos, total) = await pagamentoRepository
            .ListarPorAssinaturaAlunoPaginadoAsync(query.AssinaturaAlunoId, query.Pagina, query.TamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        return new ListarPagamentosAssinaturaAlunoResponse(
            pagamentos.Select(PagamentoResponseExtensions.ToResponseAluno).ToList(),
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
