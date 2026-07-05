using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PlanoPlataformaRepository(AppDbContext context) : IPlanoPlataformaRepository
{
    public async Task<PlanoPlataforma?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.PlanosPlataforma
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PlanoPlataforma>> ListarAsync(CancellationToken cancellationToken = default) =>
        await context.PlanosPlataforma
            .AsNoTracking()
            .OrderBy(p => p.MaxAlunos).ThenBy(p => p.Preco)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<PlanoPlataforma?> ObterPlanoFreeAsync(CancellationToken cancellationToken = default) =>
        await context.PlanosPlataforma
            .Where(p => p.Tier == TierPlano.Free && p.IsAtivo)
            .OrderBy(p => p.Preco)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(PlanoPlataforma plano, CancellationToken cancellationToken = default) =>
        await context.PlanosPlataforma.AddAsync(plano, cancellationToken).ConfigureAwait(false);
}
