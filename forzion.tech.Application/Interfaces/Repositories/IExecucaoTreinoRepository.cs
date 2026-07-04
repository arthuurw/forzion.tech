using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public record ExecucaoComNome(
    Guid ExecucaoId,
    Guid TreinoId,
    Guid AlunoId,
    DateTime DataExecucao,
    string? Observacao,
    DateTime CreatedAt,
    string NomeTreino,
    int TotalExercicios,
    int TotalSeries);

/// <summary>
/// Flat aggregated row returned by the SQL-side progressão query.
/// One row per (NomeExercicio, GrupoMuscular, Data).
/// </summary>
public record ProgressaoAggRow(
    string NomeExercicio,
    string GrupoMuscular,
    DateTime Data,
    decimal? CargaMaxima,
    double MediaSeries,
    double MediaRepeticoes);

public record SessaoDiaCount(DateTime Dia, int Total);

public record AderenciaAlunoSnapshot(
    Guid AlunoId,
    Guid ContaId,
    DateOnly UltimaExecucao,
    int Streak);

public record DigestTreinadorSnapshot(
    Guid TreinadorId,
    Guid TreinadorContaId,
    int Treinaram,
    int NaoTreinaram);

public interface IExecucaoTreinoRepository
{
    Task AdicionarAsync(ExecucaoTreino execucao, CancellationToken cancellationToken = default);
    Task<ExecucaoTreino?> ObterPorIdempotencyKeyAsync(Guid alunoId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<bool> ExisteParaTreinoComAlunoAtivoAsync(Guid treinoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoTreino>> ListarPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<int> ContarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoComNome>> ListarComNomePorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the per-day session count in [de, ate), aggregated in SQL (GROUP BY day).
    /// Bounded by the window; week bucketing is done in the application layer.
    /// </summary>
    Task<IReadOnlyList<SessaoDiaCount>> ContarSessoesPorDiaAsync(Guid alunoId, DateTime de, DateTime ate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projeção set-based p/ o scan de engajamento: por aluno com vínculo ativo e ao menos uma
    /// execução, a última data de execução + streak (dias consecutivos terminando nela, janela
    /// limitada). Duas queries agregadas (sem N+1); streak calculado em memória sobre os dias distintos.
    /// </summary>
    Task<IReadOnlyList<AderenciaAlunoSnapshot>> ProjetarAderenciaAtivosAsync(DateOnly hoje, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projeção set-based p/ o digest diário: por treinador com ≥1 vínculo ativo, quantos alunos
    /// treinaram (ao menos uma execução em <paramref name="hoje"/> UTC) e quantos não treinaram.
    /// Uma query com EXISTS correlacionado + agregação em memória (sem N+1).
    /// </summary>
    Task<IReadOnlyList<DigestTreinadorSnapshot>> ProjetarDigestTreinadoresAsync(DateOnly hoje, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-(exercício, grupoMuscular, date) aggregated rows in the period,
    /// ordered by grupoMuscular, nomeExercicio, data.
    /// Aggregation is performed in SQL (GROUP BY) to avoid full hydration.
    /// </summary>
    Task<IReadOnlyList<ProgressaoAggRow>> ProjetarProgressaoAsync(
        Guid alunoId, DateTime de, DateTime ate, CancellationToken cancellationToken = default);

    /// <summary>LGPD: zera Observacao de todas as execuções do aluno em um único UPDATE (sem hidratar entidades).</summary>
    Task AnonimizarObservacoesPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task ExcluirPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
}
