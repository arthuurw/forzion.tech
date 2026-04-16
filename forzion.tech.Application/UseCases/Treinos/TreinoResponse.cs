using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinos;

public record TreinoExercicioResponse(
    Guid TreinoExercicioId,
    Guid ExercicioId,
    int Series,
    int Repeticoes,
    decimal? Carga,
    int? Descanso,
    int Ordem);

public record TreinoResponse(
    Guid TreinoId,
    string Nome,
    ObjetivoTreino Objetivo,
    Guid TenantId,
    Guid TreinadorId,
    IReadOnlyList<TreinoExercicioResponse> Exercicios,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public static class TreinoResponseExtensions
{
    public static TreinoResponse ToResponse(Treino treino) => new(
        treino.Id,
        treino.Nome,
        treino.Objetivo,
        treino.TenantId,
        treino.TreinadorId,
        treino.Exercicios.Select(te => new TreinoExercicioResponse(
            te.Id, te.ExercicioId, te.Series, te.Repeticoes, te.Carga, te.Descanso, te.Ordem
        )).ToList(),
        treino.CreatedAt,
        treino.UpdatedAt);
}
