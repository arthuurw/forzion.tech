using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ReconciliacaoStripeEstadoRepository(AppDbContext context) : IReconciliacaoStripeEstadoRepository
{
    public async Task<ReconciliacaoStripeEstado?> ObterAsync(CancellationToken cancellationToken = default) =>
        await context.ReconciliacoesStripeEstado
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task SalvarAsync(ReconciliacaoStripeEstado estado, CancellationToken cancellationToken = default)
    {
        if (context.Entry(estado).State == EntityState.Detached)
            await context.ReconciliacoesStripeEstado.AddAsync(estado, cancellationToken).ConfigureAwait(false);
    }
}
