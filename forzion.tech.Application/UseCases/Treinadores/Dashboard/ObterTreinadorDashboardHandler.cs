using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.Dashboard;

public class ObterTreinadorDashboardHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinoRepository treinoRepository,
    IAssinaturaTreinadorRepository assinaturaRepository,
    VerificarOnboardingTreinadorHandler onboardingHandler,
    IUserContext userContext)
{
    private const int PendentesCap = 10;

    public virtual async Task<TreinadorDashboardResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var treinadorId = userContext.PerfilId;

        var contagem = await vinculoRepository
            .ContarPorStatusAsync(treinadorId, cancellationToken).ConfigureAwait(false);

        var counts = new TreinadorDashboardCounts(
            contagem.GetValueOrDefault(VinculoStatus.Ativo),
            contagem.GetValueOrDefault(VinculoStatus.AguardandoAprovacao),
            contagem.GetValueOrDefault(VinculoStatus.Inativo));

        var receita = await vinculoRepository
            .SomarReceitaPorPacoteAsync(treinadorId, cancellationToken).ConfigureAwait(false);

        var receitaPorPacote = receita
            .Select(r => new ReceitaPorPacoteItem(r.PacoteId, r.Nome, r.Alunos, r.Receita))
            .OrderByDescending(r => r.Receita)
            .ToList();

        var mrr = receita.Sum(r => r.Receita);

        var objetivosContagem = await treinoRepository
            .ContarPorObjetivoAsync(treinadorId, cancellationToken).ConfigureAwait(false);

        var objetivos = objetivosContagem
            .Select(o => new ObjetivoItem(o.Objetivo, o.Total))
            .OrderByDescending(o => o.Total)
            .ToList();

        var totalFichas = objetivosContagem.Sum(o => o.Total);

        var (pendentesItems, _) = await vinculoRepository
            .ListarComDetalhesAsync(treinadorId, VinculoStatus.AguardandoAprovacao, 1, PendentesCap, cancellationToken)
            .ConfigureAwait(false);

        var pendentes = pendentesItems.Select(VinculoDetalheResponse.De).ToList();

        var onboardingResult = await onboardingHandler
            .HandleAsync(new VerificarOnboardingTreinadorQuery(treinadorId), cancellationToken).ConfigureAwait(false);
        var onboarding = onboardingResult.IsSuccess
            ? onboardingResult.Value
            : new OnboardingStatusResponse(false, false, default, null);

        var assinatura = await assinaturaRepository
            .ObterAtualPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        var plano = new TreinadorDashboardPlano(assinatura?.Status);

        return new TreinadorDashboardResponse(
            counts,
            mrr,
            receitaPorPacote,
            totalFichas,
            objetivos,
            pendentes,
            onboarding,
            plano);
    }
}
