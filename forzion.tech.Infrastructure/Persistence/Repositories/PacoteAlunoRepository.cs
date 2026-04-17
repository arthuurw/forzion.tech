using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PacoteAlunoRepository(AppDbContext context) : IPacoteAlunoRepository
{
    public async Task<PacoteAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.PacotesAluno
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PacoteAluno>> ListarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.PacotesAluno
            .Where(p => p.TreinadorId == treinadorId)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(PacoteAluno pacote, CancellationToken cancellationToken = default) =>
        await context.PacotesAluno.AddAsync(pacote, cancellationToken).ConfigureAwait(false);
}
