using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinatura;

public class ListarPagamentosAssinaturaHandler(
    IPagamentoRepository pagamentoRepository,
    IAssinaturaRepository assinaturaRepository)
{
    public virtual async Task<IReadOnlyList<PagamentoResponse>> HandleAsync(
        ListarPagamentosAssinaturaQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var assinatura = await assinaturaRepository.ObterPorIdAsync(query.AssinaturaId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null || assinatura.AlunoId != query.AlunoId)
            throw new AcessoNegadoException();

        var pagamentos = await pagamentoRepository.ListarPorAssinaturaAsync(query.AssinaturaId, cancellationToken).ConfigureAwait(false);

        return pagamentos.Select(PagamentoResponseExtensions.ToResponseAluno).ToList();
    }
}
