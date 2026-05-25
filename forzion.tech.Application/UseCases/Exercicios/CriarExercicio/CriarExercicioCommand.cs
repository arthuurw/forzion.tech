namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public record CriarExercicioCommand(
    Guid? TreinadorId,
    string Nome,
    Guid GrupoMuscularId,
    string? Descricao);
