using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AssinaturaAlunoRepository(AppDbContext context) : IAssinaturaAlunoRepository
{
    public async Task<AssinaturaAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<AssinaturaAluno?> ObterPorVinculoIdAsync(Guid vinculoId, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .FirstOrDefaultAsync(a => a.VinculoId == vinculoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AssinaturaAluno>> ListarParaRenovarAsync(DateTime ate, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .Where(a => a.Status == AssinaturaAlunoStatus.Ativa && a.DataProximaCobranca <= ate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AssinaturaAluno>> ListarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .AsNoTracking()
            .Where(a => a.AlunoId == alunoId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(AssinaturaAluno assinatura, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos.AddAsync(assinatura, cancellationToken).ConfigureAwait(false);
}
