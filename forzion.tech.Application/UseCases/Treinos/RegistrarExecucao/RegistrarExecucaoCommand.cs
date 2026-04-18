namespace forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;

public record RegistrarExecucaoItemCommand(
    Guid TreinoExercicioId,
    int SeriesExecutadas,
    int RepeticoesExecutadas,
    decimal? CargaExecutada,
    string? Observacao);

public record RegistrarExecucaoCommand(
    Guid TreinoId,
    Guid AlunoId,
    DateTime DataExecucao,
    string? Observacao,
    IReadOnlyList<RegistrarExecucaoItemCommand> Exercicios);
