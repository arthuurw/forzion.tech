using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public record CriarExercicioCommand(
    Guid? TreinadorId,
    string Nome,
    GrupoMuscular GrupoMuscular,
    string? Descricao);
