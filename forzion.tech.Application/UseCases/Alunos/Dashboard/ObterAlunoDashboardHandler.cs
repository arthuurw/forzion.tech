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

        var (fichas, totalFichas) = await treinoAlunoRepository
            .ListarDetalhesPorAlunoAsync(alunoId, 1, MaxFichas, cancellationToken)
            .ConfigureAwait(false);

        var fichasAtivas = fichas
            .Select(f => new FichaAtivaResumo(
                f.TreinoAluno.Id, f.Treino.Id, f.Treino.Nome, f.Treino.Objetivo, f.TreinoAluno.CreatedAt))
            .ToList();

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

        var vinculoAtivo = await vinculoRepository
            .ObterAtivoPorAlunoAsync(alunoId, cancellationToken)
            .ConfigureAwait(false);
        var vinculoPendente = await vinculoRepository
            .ObterPendentePorAlunoAsync(alunoId, cancellationToken)
            .ConfigureAwait(false);

        return new ObterAlunoDashboardResponse(
            totalFichas,
            fichasAtivas,
            totalExecucoes,
            sessoesPorSemana,
            new VinculoResumo(vinculoAtivo is not null, vinculoPendente is not null));
    }
}
