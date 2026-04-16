using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public record CriarExercicioCommand(
    Guid TenantId,
    string Nome,
    GrupoMuscular GrupoMuscular,
    string? Descricao);
