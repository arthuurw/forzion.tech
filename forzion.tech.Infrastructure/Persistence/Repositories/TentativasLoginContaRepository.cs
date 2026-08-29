using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TentativasLoginContaRepository(AppDbContext context) : ITentativasLoginContaRepository
{
    public async Task<int> ObterTentativasAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.TentativasLoginConta
            .AsNoTracking()
            .Where(t => t.ContaId == contaId)
            .Select(t => t.Tentativas)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    // Upsert atômico via ON CONFLICT: dois logins concorrentes da MESMA conta não podem
    // resultar em lost update (load+increment+save perderia um incremento sob corrida) —
    // specification-concurrency §5.
    public async Task RegistrarFalhaAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO tentativas_login_conta (id, conta_id, tentativas, atualizado_em)
            VALUES ({Guid.NewGuid()}, {contaId}, 1, {agora})
            ON CONFLICT (conta_id) DO UPDATE
            SET tentativas = tentativas_login_conta.tentativas + 1, atualizado_em = {agora}
            """,
            cancellationToken)
            .ConfigureAwait(false);

    public async Task ZerarAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default) =>
        await context.TentativasLoginConta
            .Where(t => t.ContaId == contaId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.Tentativas, 0).SetProperty(t => t.AtualizadoEm, agora),
                cancellationToken)
            .ConfigureAwait(false);
}
