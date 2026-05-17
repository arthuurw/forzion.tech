using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PagamentoRepository(AppDbContext context) : IPagamentoRepository
{
    public async Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Pagamento?> ObterPorPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Pagamento>> ListarPorAssinaturaAsync(Guid assinaturaId, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .AsNoTracking()
            .Where(p => p.AssinaturaId == assinaturaId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Pagamento?> ObterPendentePorAssinaturaAsync(Guid assinaturaId, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .FirstOrDefaultAsync(p => p.AssinaturaId == assinaturaId && p.Status == PagamentoStatus.Pendente, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default) =>
        await context.Pagamentos.AddAsync(pagamento, cancellationToken).ConfigureAwait(false);
}
