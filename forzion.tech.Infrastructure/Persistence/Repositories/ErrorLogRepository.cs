using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ErrorLogRepository(AppDbContext context) : IErrorLogRepository
{
    public async Task AdicionarAsync(ErrorLogEntry entry, CancellationToken cancellationToken = default) =>
        await context.ErrorLogs.AddAsync(entry, cancellationToken).ConfigureAwait(false);

    public async Task<int> ContarDesdeAsync(DateTime desde, CancellationToken cancellationToken = default) =>
        await context.ErrorLogs
            .Where(e => e.OcorridoEm >= desde)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ErrorLogEntry>> ListarDesdeAsync(DateTime desde, int limite, CancellationToken cancellationToken = default) =>
        await context.ErrorLogs
            .AsNoTracking()
            .Where(e => e.OcorridoEm >= desde)
            .OrderByDescending(e => e.OcorridoEm)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
