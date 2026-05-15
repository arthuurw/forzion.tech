namespace forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

public record PontoProgressao(
    DateTime Data,
    decimal? CargaMaxima,
    int SeriesExecutadas,
    int RepeticoesExecutadas);

public record ExercicioProgressao(
    string NomeExercicio,
    string GrupoMuscular,
    IReadOnlyList<PontoProgressao> Historico);

public record ProgressaoAlunoResponse(
    IReadOnlyList<ExercicioProgressao> Exercicios);
