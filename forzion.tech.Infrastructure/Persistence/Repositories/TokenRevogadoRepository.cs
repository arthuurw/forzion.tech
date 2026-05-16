using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TokenRevogadoRepository(AppDbContext context) : ITokenRevogadoRepository
{
    public async Task AdicionarAsync(TokenRevogado token, CancellationToken cancellationToken = default) =>
        await context.TokensRevogados.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task<bool> EstaRevogadoAsync(Guid jti, CancellationToken cancellationToken = default) =>
        await context.TokensRevogados
            .AnyAsync(t => t.Jti == jti && t.ExpiraEm > DateTime.UtcNow, cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> LimparExpiradosAsync(CancellationToken cancellationToken = default) =>
        await context.TokensRevogados
            .Where(t => t.ExpiraEm <= DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
