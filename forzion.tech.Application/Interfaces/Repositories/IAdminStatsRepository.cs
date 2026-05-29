using forzion.tech.Application.UseCases.Admin.Stats;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAdminStatsRepository
{
    Task<IReadOnlyList<PlanoDistribuicaoItem>> ObterDistribuicaoPorPlanoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlunoFinalidadeItem>> ObterDistribuicaoPorFinalidadeAsync(CancellationToken cancellationToken = default);
}
