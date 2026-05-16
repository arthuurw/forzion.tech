using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public record CriarExercicioCommand(
    Guid? TreinadorId,
    string Nome,
    TipoGrupoMuscular GrupoMuscular,
    string? Descricao);
