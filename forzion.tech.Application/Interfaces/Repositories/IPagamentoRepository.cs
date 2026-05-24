using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Pagamento?> ObterPorPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pagamento>> ListarPorAssinaturaAlunoAsync(Guid assinaturaId, CancellationToken cancellationToken = default);
    Task<Pagamento?> ObterPendentePorAssinaturaAlunoAsync(Guid assinaturaId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default);
}
