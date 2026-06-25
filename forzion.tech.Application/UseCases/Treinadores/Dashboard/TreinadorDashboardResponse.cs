using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.Dashboard;

public record TreinadorDashboardCounts(int Ativos, int Aguardando, int Inativos);

public record ReceitaPorPacoteItem(Guid PacoteId, string Nome, int Alunos, decimal Receita);

public record ObjetivoItem(ObjetivoTreino Objetivo, int Total);

public record TreinadorDashboardPlano(AssinaturaTreinadorStatus? Status);

public record TreinadorDashboardResponse(
    TreinadorDashboardCounts Counts,
    decimal Mrr,
    IReadOnlyList<ReceitaPorPacoteItem> ReceitaPorPacote,
    int TotalFichas,
    IReadOnlyList<ObjetivoItem> Objetivos,
    IReadOnlyList<VinculoDetalheResponse> Pendentes,
    OnboardingStatusResponse Onboarding,
    TreinadorDashboardPlano Plano);
