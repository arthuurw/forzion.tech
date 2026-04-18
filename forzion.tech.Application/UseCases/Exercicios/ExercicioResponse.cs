using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Exercicios;

public record ExercicioResponse(
    Guid ExercicioId,
    string Nome,
    GrupoMuscular GrupoMuscular,
    string? Descricao,
    Guid? TreinadorId,
    bool IsGlobal,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public static class ExercicioResponseExtensions
{
    public static ExercicioResponse ToResponse(Exercicio exercicio) => new(
        exercicio.Id,
        exercicio.Nome,
        exercicio.GrupoMuscular,
        exercicio.Descricao,
        exercicio.TreinadorId,
        exercicio.IsGlobal,
        exercicio.CreatedAt,
        exercicio.UpdatedAt);
}
