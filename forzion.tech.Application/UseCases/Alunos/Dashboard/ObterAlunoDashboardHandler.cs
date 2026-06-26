using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Alunos.Dashboard;

public class ObterAlunoDashboardHandler(
    ITreinoAlunoRepository treinoAlunoRepository,
    IExecucaoTreinoRepository execucaoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext,
    TimeProvider timeProvider)
{
    private const int Semanas = 8;
    private const int MaxFichas = 5;

    public virtual async Task<ObterAlunoDashboardResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var alunoId = userContext.PerfilId;

        var fichasAtivas = await treinoAlunoRepository
            .ListarFichasResumoPorAlunoAsync(alunoId, MaxFichas, cancellationToken)
            .ConfigureAwait(false);

        var totalFichas = await treinoAlunoRepository
            .ContarAtivosPorAlunoAsync(alunoId, cancellationToken)
            .ConfigureAwait(false);

        var totalExecucoes = await execucaoRepository
            .ContarPorAlunoAsync(alunoId, cancellationToken)
            .ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var de = agora.Date.AddDays(-7 * Semanas);
        var ate = agora.Date.AddDays(1);
        var dias = await execucaoRepository
            .ContarSessoesPorDiaAsync(alunoId, de, ate, cancellationToken)
            .ConfigureAwait(false);
        var sessoesPorSemana = SessoesPorSemanaCalculator.Bucketizar(agora, Semanas, dias);

        var (vinculoAtivo, vinculoPendente) = await vinculoRepository
            .ObterResumoVinculoPorAlunoAsync(alunoId, cancellationToken)
            .ConfigureAwait(false);

        return new ObterAlunoDashboardResponse(
            totalFichas,
            fichasAtivas,
            totalExecucoes,
            sessoesPorSemana,
            new VinculoResumo(vinculoAtivo, vinculoPendente));
    }
}
