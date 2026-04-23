using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;

public record AtualizarExercicioCommand(
    Guid ExercicioId,
    Guid? TreinadorId,
    string? Nome,
    GrupoMuscular? GrupoMuscular,
    string? Descricao);
