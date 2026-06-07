using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PagamentoTreinadorRepository(AppDbContext context) : IPagamentoTreinadorRepository
{
    public async Task<PagamentoTreinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.PagamentosTreinador
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PagamentoTreinador?> ObterPorStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken cancellationToken = default) =>
        await context.PagamentosTreinador
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == stripePaymentIntentId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PagamentoTreinador?> ObterPendentePorAssinaturaAsync(Guid assinaturaTreinadorId, CancellationToken cancellationToken = default) =>
        await context.PagamentosTreinador
            .FirstOrDefaultAsync(p => p.AssinaturaTreinadorId == assinaturaTreinadorId && p.Status == PagamentoStatus.Pendente, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PagamentoTreinador?> ObterPagoPorAssinaturaAsync(Guid assinaturaTreinadorId, CancellationToken cancellationToken = default) =>
        await context.PagamentosTreinador
            .Where(p => p.AssinaturaTreinadorId == assinaturaTreinadorId
                && p.Status == PagamentoStatus.Pago
                && p.StripePaymentIntentId != null)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(PagamentoTreinador pagamento, CancellationToken cancellationToken = default) =>
        await context.PagamentosTreinador.AddAsync(pagamento, cancellationToken).ConfigureAwait(false);
}
