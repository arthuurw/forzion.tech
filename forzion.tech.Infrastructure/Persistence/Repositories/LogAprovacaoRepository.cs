using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class LogAprovacaoRepository(AppDbContext context) : ILogAprovacaoRepository
{
    public async Task AdicionarAsync(LogAprovacao log, CancellationToken cancellationToken = default) =>
        await context.LogsAprovacao.AddAsync(log, cancellationToken).ConfigureAwait(false);
}
