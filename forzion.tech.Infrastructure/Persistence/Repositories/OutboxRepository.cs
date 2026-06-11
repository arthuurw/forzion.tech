using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class OutboxRepository(AppDbContext context) : IOutboxRepository
{
    public void Enfileirar(OutboxEfeito efeito) => context.OutboxEfeitos.Add(efeito);

    public async Task<IReadOnlyList<OutboxEfeito>> ObterProcessaveisAsync(int max, DateTime agora, CancellationToken cancellationToken = default)
    {
        // FromSql parametriza os valores; o status é o nome do enum (HasConversion<string>).
        // FOR UPDATE SKIP LOCKED: workers concorrentes pulam itens já travados por outra transação.
        var status = nameof(OutboxStatus.Pendente);
        return await context.OutboxEfeitos
            .FromSqlInterpolated($@"
                SELECT * FROM outbox_efeitos
                WHERE status = {status}
                  AND proxima_tentativa <= {agora}
                ORDER BY proxima_tentativa
                LIMIT {max}
                FOR UPDATE SKIP LOCKED")
            .AsTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> LimparConcluidosAnterioresAsync(DateTime limite, CancellationToken cancellationToken = default) =>
        context.OutboxEfeitos
            .Where(e => e.Status == OutboxStatus.Concluido && e.ProcessadoEm != null && e.ProcessadoEm < limite)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<OutboxStatus, int>> ContarPorStatusAsync(CancellationToken cancellationToken = default)
    {
        var grupos = await context.OutboxEfeitos
            .GroupBy(e => e.Status)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return grupos.ToDictionary(g => g.Key, g => g.Total);
    }

    public async Task<IReadOnlyList<OutboxEfeito>> ListarPorStatusAsync(OutboxStatus status, int max, CancellationToken cancellationToken = default) =>
        await context.OutboxEfeitos
            .AsNoTracking()
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.CriadoEm)
            .Take(max)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
