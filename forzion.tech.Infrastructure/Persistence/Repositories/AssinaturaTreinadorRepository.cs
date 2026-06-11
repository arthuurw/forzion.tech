using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AssinaturaTreinadorRepository(AppDbContext context) : IAssinaturaTreinadorRepository
{
    public async Task<AssinaturaTreinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.AssinaturasTreinador
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<AssinaturaTreinador?> ObterAtualPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.AssinaturasTreinador
            .Where(a => a.TreinadorId == treinadorId && a.Status != AssinaturaTreinadorStatus.Cancelada)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    // Keyset por Id (uuid ordenável): renovação bem-sucedida tira a linha do filtro → offset pularia
    // não-processados. Cursor avança mesmo em falha → não reprocessa no mesmo run (próximo cron pega).
    public async Task<IReadOnlyList<AssinaturaTreinador>> ListarParaRenovarAsync(DateTime ate, Guid? aposId, int limite, CancellationToken cancellationToken = default) =>
        await context.AssinaturasTreinador
            .AsNoTracking()
            .Where(a => (a.Status == AssinaturaTreinadorStatus.Ativa || a.Status == AssinaturaTreinadorStatus.Inadimplente)
                        && a.DataProximaCobranca <= ate
                        && (aposId == null || a.Id > aposId))
            .OrderBy(a => a.Id)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AssinaturaTreinador>> ListarParaPreAvisoAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken = default) =>
        await context.AssinaturasTreinador
            .AsNoTracking()
            .Where(a => a.Status == AssinaturaTreinadorStatus.Ativa
                        && a.DataProximaCobranca >= inicio && a.DataProximaCobranca < fim)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(AssinaturaTreinador assinatura, CancellationToken cancellationToken = default) =>
        await context.AssinaturasTreinador.AddAsync(assinatura, cancellationToken).ConfigureAwait(false);
}
