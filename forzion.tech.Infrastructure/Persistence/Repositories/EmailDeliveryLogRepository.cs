using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class EmailDeliveryLogRepository(AppDbContext context) : IEmailDeliveryLogRepository
{
    public async Task AdicionarAsync(EmailDeliveryLog log, CancellationToken cancellationToken = default) =>
        await context.EmailDeliveryLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
}
