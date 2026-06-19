using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.UseCases.Exercicios;

public record ExercicioResponse(
    Guid ExercicioId,
    string Nome,
    Guid GrupoMuscularId,
    string GrupoMuscular,
    string? Descricao,
    string? ComoExecutar,
    string? VideoId,
    Guid? TreinadorId,
    bool IsGlobal,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public static class ExercicioResponseExtensions
{
    public static ExercicioResponse ToResponse(Exercicio exercicio, string grupoMuscularNome) => new(
        exercicio.Id,
        exercicio.Nome,
        exercicio.GrupoMuscularId,
        grupoMuscularNome,
        exercicio.Descricao,
        exercicio.ComoExecutar,
        exercicio.VideoId,
        exercicio.TreinadorId,
        exercicio.IsGlobal,
        exercicio.CreatedAt,
        exercicio.UpdatedAt);
}
