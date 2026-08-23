using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class BloqueioAgendaRepository(AppDbContext context) : IBloqueioAgendaRepository
{
    public async Task<IReadOnlyList<BloqueioAgenda>> ListarVigentesAsync(Guid treinadorId, DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await context.BloqueiosAgenda
            .AsNoTracking()
            .Where(b => b.TreinadorId == treinadorId
                && (b.Tipo == TipoBloqueio.RecorrenteSemanal || (b.InicioUtc < ateUtc && b.FimUtc > deUtc)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<BloqueioAgenda>> ListarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.BloqueiosAgenda
            .AsNoTracking()
            .Where(b => b.TreinadorId == treinadorId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    // Tracked (não AsNoTracking): usado pelo caminho de mutação (remoção) do treinador — precedente
    // fatia 2 (LeadRepository.ObterComHistoricoAsync).
    public async Task<BloqueioAgenda?> ObterPorIdAsync(Guid id, Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.BloqueiosAgenda
            .FirstOrDefaultAsync(b => b.Id == id && b.TreinadorId == treinadorId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(BloqueioAgenda bloqueio, CancellationToken cancellationToken = default) =>
        await context.BloqueiosAgenda.AddAsync(bloqueio, cancellationToken).ConfigureAwait(false);

    public void Remover(BloqueioAgenda bloqueio) => context.BloqueiosAgenda.Remove(bloqueio);
}
