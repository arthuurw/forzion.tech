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

public interface IExecucaoTreinoRepository
{
    Task AdicionarAsync(ExecucaoTreino execucao, CancellationToken cancellationToken = default);
    Task<ExecucaoTreino?> ObterPorIdempotencyKeyAsync(Guid alunoId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<bool> ExisteParaTreinoComAlunoAtivoAsync(Guid treinoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoTreino>> ListarPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<int> ContarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoComNome>> ListarComNomePorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-(exercício, grupoMuscular, date) aggregated rows in the period,
    /// ordered by grupoMuscular, nomeExercicio, data.
    /// Aggregation is performed in SQL (GROUP BY) to avoid full hydration.
    /// </summary>
    Task<IReadOnlyList<ProgressaoAggRow>> ProjetarProgressaoAsync(
        Guid alunoId, DateTime de, DateTime ate, CancellationToken cancellationToken = default);

    /// <summary>LGPD ANON-02: zera Observacao de todas as execuções do aluno em um único UPDATE (sem hidratar entidades).</summary>
    Task AnonimizarObservacoesPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task ExcluirPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
}
