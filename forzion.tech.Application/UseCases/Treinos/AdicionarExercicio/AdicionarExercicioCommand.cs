namespace forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;

public record AdicionarExercicioCommand(
    Guid TreinoId,
    Guid ExercicioId,
    int Series,
    int Repeticoes,
    decimal? Carga,
    int? Descanso);
