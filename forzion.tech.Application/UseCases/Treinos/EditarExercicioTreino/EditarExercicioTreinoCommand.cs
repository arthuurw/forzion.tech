namespace forzion.tech.Application.UseCases.Treinos.EditarExercicioTreino;

public record SerieConfigEditCommand(
    int Quantidade,
    int RepeticoesMin,
    int? RepeticoesMax,
    string? Descricao,
    decimal? Carga,
    int? Descanso);

public record EditarExercicioTreinoCommand(
    Guid TreinoId,
    Guid TreinoExercicioId,
    IReadOnlyList<SerieConfigEditCommand> Series);
