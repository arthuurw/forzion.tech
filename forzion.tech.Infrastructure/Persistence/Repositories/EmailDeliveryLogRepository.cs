using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class EmailDeliveryLogRepository(AppDbContext context) : IEmailDeliveryLogRepository
{
    public async Task AdicionarAsync(EmailDeliveryLog log, CancellationToken cancellationToken = default) =>
        await context.EmailDeliveryLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<string, int>> ContarPorEventoDesdeAsync(DateTime desde, CancellationToken cancellationToken = default) =>
        await context.EmailDeliveryLogs
            .Where(e => e.OcorridoEm >= desde)
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventType, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
}
