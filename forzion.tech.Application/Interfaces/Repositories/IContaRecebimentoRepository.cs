using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IContaRecebimentoRepository
{
    Task<ContaRecebimento?> ObterPorTreinadorIdAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<ContaRecebimento?> ObterPorStripeAccountIdAsync(string stripeAccountId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(ContaRecebimento conta, CancellationToken cancellationToken = default);
}
