using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Admin.Stats;

public class ObterDashboardStatsHandler(IAdminStatsRepository statsRepository)
{
    public virtual async Task<DashboardStatsResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var planoDistribuicao = await statsRepository
            .ObterDistribuicaoPorPlanoAsync(cancellationToken)
            .ConfigureAwait(false);

        var alunoFinalidade = await statsRepository
            .ObterDistribuicaoPorFinalidadeAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DashboardStatsResponse(planoDistribuicao, alunoFinalidade);
    }
}
