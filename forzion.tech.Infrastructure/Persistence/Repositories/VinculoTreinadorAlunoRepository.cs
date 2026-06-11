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
            .AsNoTracking()
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

    public async Task<bool> TemVinculosAtivosAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .AnyAsync(v => v.TreinadorId == treinadorId
                && (v.Status == VinculoStatus.Ativo || v.Status == VinculoStatus.AguardandoAprovacao), cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<VinculoTreinadorAluno>> ListarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .Where(v => v.TreinadorId == treinadorId && v.Status == VinculoStatus.Ativo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<VinculoComDetalheAluno> Items, int Total)> ListarComDetalhesAsync(
        Guid treinadorId, VinculoStatus? status, int pagina, int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var alunosComVinculoAtivo = context.VinculosTreinadorAluno
            .Where(v2 => v2.Status == VinculoStatus.Ativo && v2.TreinadorId != treinadorId)
            .Select(v2 => v2.AlunoId)
            .Distinct();

        var baseQuery =
            from v in context.VinculosTreinadorAluno
            join a in context.Alunos on v.AlunoId equals a.Id
            where v.TreinadorId == treinadorId
            select new
            {
                v,
                a,
                TemVinculoAtivoPrevio = alunosComVinculoAtivo.Contains(v.AlunoId)
            };

        if (status.HasValue)
            baseQuery = baseQuery.Where(x => x.v.Status == status.Value);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var raw = await baseQuery
            .AsNoTracking()
            .OrderByDescending(x => x.v.CreatedAt)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = raw.Select(x => new VinculoComDetalheAluno(x.v, x.a.Nome, x.a.Email?.Value, x.TemVinculoAtivoPrevio))
            .ToList();

        return (items, total);
    }

    public async Task<VinculoTreinadorAluno?> ObterPendentePorParAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .FirstOrDefaultAsync(v => v.TreinadorId == treinadorId && v.AlunoId == alunoId && v.Status == VinculoStatus.AguardandoAprovacao, cancellationToken)
            .ConfigureAwait(false);

    public async Task<VinculoTreinadorAluno?> ObterPendentePorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .AsNoTracking()
            .Where(v => v.AlunoId == alunoId && v.Status == VinculoStatus.AguardandoAprovacao)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(VinculoTreinadorAluno vinculo, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno.AddAsync(vinculo, cancellationToken).ConfigureAwait(false);

    // Sem AsNoTracking: handler de anonimização (ANON-01) precisa chamar Inativar nas entidades retornadas.
    public async Task<IReadOnlyList<VinculoTreinadorAluno>> ListarAtivosEPendentesPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .Where(v => v.AlunoId == alunoId &&
                        (v.Status == VinculoStatus.Ativo || v.Status == VinculoStatus.AguardandoAprovacao))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<VinculoTreinadorAluno>> ListarTodosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .AsNoTracking()
            .Where(v => v.AlunoId == alunoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<VinculoTreinadorAluno>> ListarTodosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.VinculosTreinadorAluno
            .AsNoTracking()
            .Where(v => v.TreinadorId == treinadorId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
