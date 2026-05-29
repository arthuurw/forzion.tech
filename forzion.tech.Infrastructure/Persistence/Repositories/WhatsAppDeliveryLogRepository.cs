using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class WhatsAppDeliveryLogRepository(AppDbContext context) : IWhatsAppDeliveryLogRepository
{
    public async Task AdicionarAsync(WhatsAppDeliveryLog log, CancellationToken cancellationToken = default) =>
        await context.WhatsAppDeliveryLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);

    public Task<bool> ExisteAsync(string metaMessageId, string eventType, CancellationToken cancellationToken = default) =>
        context.WhatsAppDeliveryLogs
            .AsNoTracking()
            .AnyAsync(e => e.MetaMessageId == metaMessageId && e.EventType == eventType, cancellationToken);
}
