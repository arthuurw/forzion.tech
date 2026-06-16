using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class EmailDeliveryLogRepository(AppDbContext context, IRecipientHasher hasher) : IEmailDeliveryLogRepository
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

    public Task<bool> ExisteAsync(string resendMessageId, string eventType, CancellationToken cancellationToken = default) =>
        context.EmailDeliveryLogs
            .AsNoTracking()
            .AnyAsync(e => e.ResendMessageId == resendMessageId && e.EventType == eventType, cancellationToken);

    public async Task<IReadOnlyList<EmailDeliveryLog>> ListarPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var hash = hasher.Hash(email);
        return await context.EmailDeliveryLogs
            .AsNoTracking()
            .Where(e => e.RecipientEmailHash == hash)
            .OrderByDescending(e => e.OcorridoEm)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AnonimizarPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var hash = hasher.Hash(email);
        await context.EmailDeliveryLogs
            .Where(e => e.RecipientEmailHash == hash)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.RecipientEmailHash, "(anonimizado)"),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
