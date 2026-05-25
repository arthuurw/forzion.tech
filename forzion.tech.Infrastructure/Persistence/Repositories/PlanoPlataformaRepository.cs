using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
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
            .OrderBy(p => p.MaxAlunos).ThenBy(p => p.Preco)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(PlanoPlataforma plano, CancellationToken cancellationToken = default) =>
        await context.PlanosPlataforma.AddAsync(plano, cancellationToken).ConfigureAwait(false);
}
