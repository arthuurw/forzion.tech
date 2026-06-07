using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPagamentoTreinadorRepository
{
    Task<PagamentoTreinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagamentoTreinador?> ObterPorStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken cancellationToken = default);
    Task<PagamentoTreinador?> ObterPendentePorAssinaturaAsync(Guid assinaturaTreinadorId, CancellationToken cancellationToken = default);
    Task<PagamentoTreinador?> ObterPagoPorAssinaturaAsync(Guid assinaturaTreinadorId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(PagamentoTreinador pagamento, CancellationToken cancellationToken = default);
}
