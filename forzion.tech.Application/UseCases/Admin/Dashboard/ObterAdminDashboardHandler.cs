using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Admin.Dashboard;

public class ObterAdminDashboardHandler(
    ITreinadorRepository treinadorRepository,
    IAlunoRepository alunoRepository,
    IPlanoPlataformaRepository planoRepository,
    IExercicioRepository exercicioRepository,
    IGrupoMuscularRepository grupoMuscularRepository,
    IAdminStatsRepository statsRepository)
{
    private const int PendentesCap = 20;
    private const int RecentesCap = 5;

    public virtual async Task<AdminDashboardResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var treinadoresAtivos = await treinadorRepository
            .ContarPorStatusAsync(TreinadorStatus.Ativo, cancellationToken).ConfigureAwait(false);
        var treinadoresPendentes = await treinadorRepository
            .ContarPorStatusAsync(TreinadorStatus.AguardandoAprovacao, cancellationToken).ConfigureAwait(false);
        var treinadoresInativos = await treinadorRepository
            .ContarPorStatusAsync(TreinadorStatus.Inativo, cancellationToken).ConfigureAwait(false);

        var alunosAtivos = await alunoRepository
            .ContarPorStatusAsync(AlunoStatus.Ativo, cancellationToken).ConfigureAwait(false);
        var alunosPendentes = await alunoRepository
            .ContarPorStatusAsync(AlunoStatus.AguardandoAprovacao, cancellationToken).ConfigureAwait(false);
        var alunosInativos = await alunoRepository
            .ContarPorStatusAsync(AlunoStatus.Inativo, cancellationToken).ConfigureAwait(false);

        var (treinadoresPendentesItems, _) = await treinadorRepository
            .ListarAsync(TreinadorStatus.AguardandoAprovacao, 1, PendentesCap, cancellationToken).ConfigureAwait(false);
        var (alunosPendentesItems, _) = await alunoRepository
            .ListarTodosAsync(1, PendentesCap, null, AlunoStatus.AguardandoAprovacao, cancellationToken).ConfigureAwait(false);

        var recentes = await treinadorRepository
            .ListarRecentesAsync(RecentesCap, cancellationToken).ConfigureAwait(false);

        var planos = await planoRepository.ListarAsync(cancellationToken).ConfigureAwait(false);
        var exerciciosGlobais = await exercicioRepository.ContarGlobaisAsync(cancellationToken).ConfigureAwait(false);
        var gruposMusculares = await grupoMuscularRepository.ContarAsync(cancellationToken).ConfigureAwait(false);

        var planoDistribuicao = await statsRepository
            .ObterDistribuicaoPorPlanoAsync(cancellationToken).ConfigureAwait(false);
        var alunoFinalidade = await statsRepository
            .ObterDistribuicaoPorFinalidadeAsync(cancellationToken).ConfigureAwait(false);

        return new AdminDashboardResponse(
            new AdminDashboardCounts(treinadoresAtivos, treinadoresPendentes, treinadoresInativos),
            new AdminDashboardCounts(alunosAtivos, alunosPendentes, alunosInativos),
            new AdminDashboardTotals(planos.Count, exerciciosGlobais, gruposMusculares),
            planoDistribuicao,
            alunoFinalidade,
            [.. treinadoresPendentesItems.Select(TreinadorResponse.De)],
            [.. alunosPendentesItems.Select(a => CadastrarAlunoHandler.ToResponse(a))],
            [.. recentes.Select(TreinadorResponse.De)],
            [.. planos.Select(PlanoPlataformaResponseExtensions.ToResponse)]);
    }
}
