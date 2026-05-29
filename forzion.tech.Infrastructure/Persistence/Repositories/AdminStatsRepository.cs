using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.Stats;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AdminStatsRepository(AppDbContext context) : IAdminStatsRepository
{
    private readonly AppDbContext _context = context;

    public async Task<IReadOnlyList<PlanoDistribuicaoItem>> ObterDistribuicaoPorPlanoAsync(CancellationToken cancellationToken = default)
    {
        // GROUP BY plano tier: count treinadores per plan tier using SQL GROUP BY.
        // Treinadores without a plan are grouped under a dedicated "SemPlano" bucket.
        // We project to (TierOrNull, Count) in SQL, then map TierPlano? → string client-side.
        var rows = await (
            from t in _context.Treinadores.AsNoTracking()
            join p in _context.PlanosPlataforma.AsNoTracking()
                on t.PlanoPlataformaId equals p.Id into planos
            from p in planos.DefaultIfEmpty()
            group t by (TierPlano?)p.Tier into g
            select new { Tier = g.Key, Total = g.Count() }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows
            .Select(r => new PlanoDistribuicaoItem(
                r.Tier.HasValue ? r.Tier.Value.ToString() : "SemPlano",
                r.Total))
            .ToList();
    }

    public async Task<IReadOnlyList<AlunoFinalidadeItem>> ObterDistribuicaoPorFinalidadeAsync(CancellationToken cancellationToken = default)
    {
        // GROUP BY aluno finalidade: count alunos per FinalidadeTreino value using SQL GROUP BY.
        // Alunos with null finalidade are grouped under "NaoInformado" (client-side mapping).
        var rows = await (
            from a in _context.Alunos.AsNoTracking()
            group a by a.Finalidade into g
            select new { Finalidade = g.Key, Total = g.Count() }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows
            .Select(r => new AlunoFinalidadeItem(
                r.Finalidade.HasValue ? r.Finalidade.Value.ToString() : "NaoInformado",
                r.Total))
            .ToList();
    }
}
