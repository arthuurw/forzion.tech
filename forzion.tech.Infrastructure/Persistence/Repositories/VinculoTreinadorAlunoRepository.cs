using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class VinculoTreinadorAlunoRepository(AppDbContext context) : IVinculoTreinadorAlunoRepository
{
    public async Task<VinculoTreinadorAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<VinculoTreinadorAluno?> ObterAtivoAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .FirstOrDefaultAsync(v => v.TreinadorId == treinadorId && v.AlunoId == alunoId && v.Status == VinculoStatus.Ativo, cancellationToken)
            .ConfigureAwait(false);

    public async Task<VinculoTreinadorAluno?> ObterAtivoPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .FirstOrDefaultAsync(v => v.AlunoId == alunoId && v.Status == VinculoStatus.Ativo, cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> ContarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .CountAsync(v => v.TreinadorId == treinadorId && v.Status == VinculoStatus.Ativo, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<VinculoTreinadorAluno>> ListarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .Where(v => v.TreinadorId == treinadorId && v.Status == VinculoStatus.Ativo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(VinculoTreinadorAluno vinculo, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno.AddAsync(vinculo, cancellationToken).ConfigureAwait(false);
}
