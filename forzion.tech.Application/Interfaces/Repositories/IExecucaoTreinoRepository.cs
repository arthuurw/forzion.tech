using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public record ExecucaoExercicioDetalhe(
    Guid TreinoExercicioId,
    string NomeExercicio,
    string GrupoMuscular,
    int SeriesExecutadas,
    int RepeticoesExecutadas,
    decimal? CargaExecutada);

public record ExecucaoDetalheItem(
    Guid ExecucaoId,
    DateTime DataExecucao,
    Guid TreinoId,
    string? Observacao,
    IReadOnlyList<ExecucaoExercicioDetalhe> Exercicios);

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
    Task<bool> ExisteParaTreinoAsync(Guid treinoId, CancellationToken cancellationToken = default);
    Task<bool> ExisteParaTreinoComAlunoAtivoAsync(Guid treinoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoTreino>> ListarPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<int> ContarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoComNome>> ListarComNomePorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoDetalheItem>> ListarPorAlunoComExerciciosAsync(
        Guid alunoId, DateTime de, DateTime ate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-(exercício, grupoMuscular, date) aggregated rows in the period,
    /// ordered by grupoMuscular, nomeExercicio, data — matching ProgressaoProjection output.
    /// Aggregation is performed in SQL (GROUP BY) to avoid full hydration.
    /// </summary>
    Task<IReadOnlyList<ProgressaoAggRow>> ProjetarProgressaoAsync(
        Guid alunoId, DateTime de, DateTime ate, CancellationToken cancellationToken = default);
}
