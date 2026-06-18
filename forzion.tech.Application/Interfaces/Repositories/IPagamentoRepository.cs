using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Pagamento?> ObterPorPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pagamento>> ListarPorAssinaturaAlunoAsync(Guid assinaturaId, CancellationToken cancellationToken = default);
    Task<Pagamento?> ObterPendentePorAssinaturaAlunoAsync(Guid assinaturaId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default);
    Task<int> ContarPorStatusAsync(PagamentoStatus status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComissaoTreinadorPeriodo>> ListarComissaoPorTreinadorNoPeriodoAsync(
        DateTime inicio, DateTime fimExclusivo, decimal taxaPlataformaPercent,
        Guid? aposTreinadorId, int limite, CancellationToken cancellationToken = default);
}
