using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PlanoTreinadorRepository(AppDbContext context) : IPlanoTreinadorRepository
{
    public async Task<PlanoTreinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.PlanosTreinador
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PlanoTreinador>> ListarAsync(CancellationToken cancellationToken = default) =>
        await context.PlanosTreinador
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(PlanoTreinador plano, CancellationToken cancellationToken = default) =>
        await context.PlanosTreinador.AddAsync(plano, cancellationToken).ConfigureAwait(false);
}
