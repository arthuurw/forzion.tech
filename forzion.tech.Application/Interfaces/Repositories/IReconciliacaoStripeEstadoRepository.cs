using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IReconciliacaoStripeEstadoRepository
{
    Task<ReconciliacaoStripeEstado?> ObterAsync(CancellationToken cancellationToken = default);
    Task SalvarAsync(ReconciliacaoStripeEstado estado, CancellationToken cancellationToken = default);
}
