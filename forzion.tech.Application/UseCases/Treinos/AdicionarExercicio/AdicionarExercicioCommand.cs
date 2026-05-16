namespace forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;

public record SerieConfigCommand(
    int Quantidade,
    int RepeticoesMin,
    int? RepeticoesMax,
    string? Descricao,
    decimal? Carga,
    int? Descanso);

public record AdicionarExercicioCommand(
    Guid TreinoId,
    Guid ExercicioId,
    IReadOnlyList<SerieConfigCommand> Series);
