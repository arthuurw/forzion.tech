using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AssinaturaRepository(AppDbContext context) : IAssinaturaRepository
{
    public async Task<Assinatura?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Assinaturas
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Assinatura?> ObterPorVinculoIdAsync(Guid vinculoId, CancellationToken cancellationToken = default) =>
        await context.Assinaturas
            .FirstOrDefaultAsync(a => a.VinculoId == vinculoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Assinatura>> ListarParaRenovarAsync(DateTime ate, CancellationToken cancellationToken = default) =>
        await context.Assinaturas
            .Where(a => a.Status == AssinaturaStatus.Ativa && a.DataProximaCobranca <= ate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Assinatura>> ListarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.Assinaturas
            .AsNoTracking()
            .Where(a => a.AlunoId == alunoId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(Assinatura assinatura, CancellationToken cancellationToken = default) =>
        await context.Assinaturas.AddAsync(assinatura, cancellationToken).ConfigureAwait(false);
}
