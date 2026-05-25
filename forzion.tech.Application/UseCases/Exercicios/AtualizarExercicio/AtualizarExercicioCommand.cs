namespace forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;

public record AtualizarExercicioCommand(
    Guid ExercicioId,
    Guid? TreinadorId,
    string? Nome,
    Guid? GrupoMuscularId,
    string? Descricao);
