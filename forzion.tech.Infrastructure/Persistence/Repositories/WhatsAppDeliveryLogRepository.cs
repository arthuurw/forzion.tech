using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class WhatsAppDeliveryLogRepository(AppDbContext context, IRecipientHasher hasher) : IWhatsAppDeliveryLogRepository
{
    public async Task AdicionarAsync(WhatsAppDeliveryLog log, CancellationToken cancellationToken = default) =>
        await context.WhatsAppDeliveryLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);

    public Task<bool> ExisteAsync(string metaMessageId, string eventType, CancellationToken cancellationToken = default) =>
        context.WhatsAppDeliveryLogs
            .AsNoTracking()
            .AnyAsync(e => e.MetaMessageId == metaMessageId && e.EventType == eventType, cancellationToken);

    public async Task<IReadOnlyList<WhatsAppDeliveryLog>> ListarPorTelefoneAsync(string telefone, CancellationToken cancellationToken = default)
    {
        var hash = hasher.Hash(telefone);
        return await context.WhatsAppDeliveryLogs
            .AsNoTracking()
            .Where(w => w.RecipientPhoneHash == hash)
            .OrderByDescending(w => w.OcorridoEm)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AnonimizarPorTelefoneAsync(string telefone, CancellationToken cancellationToken = default)
    {
        var hash = hasher.Hash(telefone);
        await context.WhatsAppDeliveryLogs
            .Where(w => w.RecipientPhoneHash == hash)
            .ExecuteUpdateAsync(
                s => s.SetProperty(w => w.RecipientPhoneHash, "(anonimizado)"),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
