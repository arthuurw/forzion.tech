using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TokenRevogadoRepository(AppDbContext context, TimeProvider timeProvider) : ITokenRevogadoRepository
{
    public async Task AdicionarAsync(TokenRevogado token, CancellationToken cancellationToken = default) =>
        await context.TokensRevogados.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task<bool> EstaRevogadoAsync(Guid jti, CancellationToken cancellationToken = default)
    {
        // "agora" via TimeProvider (não DateTime.UtcNow) p/ testabilidade: vira parâmetro SQL.
        // Antes o Npgsql traduzia DateTime.UtcNow para now() (relógio do servidor de banco).
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        return await context.TokensRevogados
            .AnyAsync(t => t.Jti == jti && t.ExpiraEm > agora, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> LimparExpiradosAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        return await context.TokensRevogados
            .Where(t => t.ExpiraEm <= agora)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
