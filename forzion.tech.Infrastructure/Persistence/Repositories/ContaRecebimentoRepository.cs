using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ContaRecebimentoRepository(AppDbContext context) : IContaRecebimentoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<ContaRecebimento?> ObterPorTreinadorIdAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await _context.ContasRecebimento
            .FirstOrDefaultAsync(c => c.TreinadorId == treinadorId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ContaRecebimento?> ObterPorStripeAccountIdAsync(string stripeAccountId, CancellationToken cancellationToken = default) =>
        await _context.ContasRecebimento
            .FirstOrDefaultAsync(c => c.StripeConnectAccountId == stripeAccountId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ContaRecebimento>> ListarConfiguradasPendentesOnboardingAsync(int max, CancellationToken cancellationToken = default) =>
        await _context.ContasRecebimento
            .Where(c => c.StripeConnectAccountId != null && !c.OnboardingCompleto)
            .OrderBy(c => c.CreatedAt)
            .Take(max)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(ContaRecebimento conta, CancellationToken cancellationToken = default) =>
        await _context.ContasRecebimento.AddAsync(conta, cancellationToken).ConfigureAwait(false);
}
