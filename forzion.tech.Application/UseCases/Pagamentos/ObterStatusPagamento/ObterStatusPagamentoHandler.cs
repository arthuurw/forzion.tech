using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;

public class ObterStatusPagamentoHandler(
    IPagamentoRepository pagamentoRepository,
    IAssinaturaAlunoRepository assinaturaRepository)
{
    public virtual async Task<PagamentoResponse> HandleAsync(
        ObterStatusPagamentoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pagamento = await pagamentoRepository.ObterPorIdAsync(query.PagamentoId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Pagamento não encontrado.");

        var assinatura = await assinaturaRepository.ObterPorIdAsync(pagamento.AssinaturaAlunoId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null || assinatura.AlunoId != query.AlunoId)
            throw new AcessoNegadoException();

        return PagamentoResponseExtensions.ToResponseAluno(pagamento);
    }
}
