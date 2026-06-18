using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class NotaFiscalRepository(AppDbContext context) : INotaFiscalRepository
{
    public async Task AdicionarAsync(NotaFiscal notaFiscal, CancellationToken cancellationToken = default) =>
        await context.NotasFiscais.AddAsync(notaFiscal, cancellationToken).ConfigureAwait(false);

    public async Task<NotaFiscal?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.NotasFiscais
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<NotaFiscal?> ObterPorPagamentoTreinadorAsync(Guid pagamentoTreinadorId, CancellationToken cancellationToken = default) =>
        await context.NotasFiscais
            .FirstOrDefaultAsync(n => n.PagamentoTreinadorId == pagamentoTreinadorId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NotaFiscal>> ListarPorTreinadorAsync(Guid treinadorId, Guid? aposId, int limite, CancellationToken cancellationToken = default) =>
        await context.NotasFiscais
            .AsNoTracking()
            .Where(n => n.TreinadorId == treinadorId && (aposId == null || n.Id > aposId))
            .OrderBy(n => n.Id)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NotaFiscal>> ListarPorStatusAsync(NotaFiscalStatus status, Guid? aposId, int limite, CancellationToken cancellationToken = default) =>
        await context.NotasFiscais
            .Where(n => n.Status == status && (aposId == null || n.Id > aposId))
            .OrderBy(n => n.Id)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NotaFiscal>> ListarAdminAsync(NotaFiscalStatus? status, Guid? treinadorId, Guid? aposId, int limite, CancellationToken cancellationToken = default) =>
        await context.NotasFiscais
            .AsNoTracking()
            .Where(n => (status == null || n.Status == status)
                        && (treinadorId == null || n.TreinadorId == treinadorId)
                        && (aposId == null || n.Id > aposId))
            .OrderBy(n => n.Id)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> ExisteComissaoAsync(Guid treinadorId, DateOnly competenciaInicio, CancellationToken cancellationToken = default) =>
        await context.NotasFiscais
            .AnyAsync(n => n.TreinadorId == treinadorId
                           && n.Tipo == TipoNotaFiscal.ComissaoMarketplace
                           && n.CompetenciaInicio == competenciaInicio, cancellationToken)
            .ConfigureAwait(false);

    public async Task<HashSet<Guid>> ListarTreinadoresComComissaoAsync(IReadOnlyCollection<Guid> treinadorIds, DateOnly competenciaInicio, CancellationToken cancellationToken = default)
    {
        var ids = await context.NotasFiscais
            .Where(n => n.Tipo == TipoNotaFiscal.ComissaoMarketplace
                        && n.CompetenciaInicio == competenciaInicio
                        && treinadorIds.Contains(n.TreinadorId))
            .Select(n => n.TreinadorId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }
}
