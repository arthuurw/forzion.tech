using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinadorRepository(AppDbContext context) : ITreinadorRepository
{
    public async Task<Treinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Treinadores
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(Treinador treinador, CancellationToken cancellationToken = default) =>
        await context.Treinadores.AddAsync(treinador, cancellationToken).ConfigureAwait(false);
}
