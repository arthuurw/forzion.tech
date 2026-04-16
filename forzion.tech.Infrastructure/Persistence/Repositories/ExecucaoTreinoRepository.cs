using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ExecucaoTreinoRepository(AppDbContext context) : IExecucaoTreinoRepository
{
    private readonly AppDbContext _context = context;

    public async Task AdicionarAsync(ExecucaoTreino execucao, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino.AddAsync(execucao, cancellationToken).ConfigureAwait(false);

    public async Task<bool> ExisteParaTreinoAsync(Guid treinoId, CancellationToken cancellationToken = default) =>
        await _context.ExecucoesTreino
            .AnyAsync(e => e.TreinoId == treinoId, cancellationToken)
            .ConfigureAwait(false);
}
