namespace forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;

public record AdicionarExercicioCommand(
    Guid TenantId,
    Guid TreinoId,
    Guid ExercicioId,
    int Series,
    int Repeticoes,
    decimal? Carga,
    int? Descanso);
