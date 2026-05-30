using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PacoteRepository(AppDbContext context) : IPacoteRepository
{
    public async Task<Pacote?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Pacotes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Pacote>> ListarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.Pacotes
            .AsNoTracking()
            .Where(p => p.TreinadorId == treinadorId)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(Pacote pacote, CancellationToken cancellationToken = default) =>
        await context.Pacotes.AddAsync(pacote, cancellationToken).ConfigureAwait(false);

    public void Remover(Pacote pacote) =>
        context.Pacotes.Remove(pacote);

    public async Task<bool> ExisteVinculoComPacoteAsync(Guid pacoteId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .AnyAsync(v => v.PacoteId == pacoteId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Pacote>> ListarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.Pacotes
            .Where(p => p.TreinadorId == treinadorId && p.IsAtivo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
