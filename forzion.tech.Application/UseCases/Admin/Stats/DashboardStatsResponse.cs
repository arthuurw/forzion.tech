namespace forzion.tech.Application.UseCases.Admin.Stats;

public record PlanoDistribuicaoItem(string Tier, int Total);

public record AlunoFinalidadeItem(string Finalidade, int Total);

public record DashboardStatsResponse(
    IReadOnlyList<PlanoDistribuicaoItem> PlanoDistribuicao,
    IReadOnlyList<AlunoFinalidadeItem> AlunoFinalidade);
