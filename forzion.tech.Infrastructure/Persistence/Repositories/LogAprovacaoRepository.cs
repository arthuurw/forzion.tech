using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class LogAprovacaoRepository(AppDbContext context) : ILogAprovacaoRepository
{
    public async Task AdicionarAsync(LogAprovacao log, CancellationToken cancellationToken = default) =>
        await context.LogsAprovacao.AddAsync(log, cancellationToken).ConfigureAwait(false);

    public async Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.LogsAprovacao
            .Where(l => l.EntidadeId == contaId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
}
