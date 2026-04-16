namespace forzion.tech.Application.UseCases.Treinos.RemoverExercicio;

public record RemoverExercicioCommand(Guid TenantId, Guid TreinoId, Guid TreinoExercicioId);
