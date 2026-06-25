using forzion.tech.Application.UseCases.Admin.Stats;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Treinadores;

namespace forzion.tech.Application.UseCases.Admin.Dashboard;

public record AdminDashboardCounts(int Ativos, int Pendentes, int Inativos);

public record AdminDashboardTotals(int Planos, int ExerciciosGlobais, int GruposMusculares);

public record AdminDashboardResponse(
    AdminDashboardCounts Treinadores,
    AdminDashboardCounts Alunos,
    AdminDashboardTotals Totals,
    IReadOnlyList<PlanoDistribuicaoItem> PlanoDistribuicao,
    IReadOnlyList<AlunoFinalidadeItem> AlunoFinalidade,
    IReadOnlyList<TreinadorResponse> TreinadoresPendentes,
    IReadOnlyList<AlunoResponse> AlunosPendentes,
    IReadOnlyList<TreinadorResponse> RecentTreinadores,
    IReadOnlyList<PlanoPlataformaResponse> Planos);
