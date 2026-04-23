namespace forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;

public record ExcluirExercicioCommand(Guid ExercicioId, Guid? TreinadorId);
