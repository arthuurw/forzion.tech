using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinos;

public record SerieConfigResponse(
    Guid SerieConfigId,
    int Quantidade,
    int RepeticoesMin,
    int? RepeticoesMax,
    string? Descricao,
    decimal? Carga,
    int? Descanso,
    int Ordem);

public record TreinoExercicioResponse(
    Guid TreinoExercicioId,
    Guid ExercicioId,
    string NomeExercicio,
    IReadOnlyList<SerieConfigResponse> Series,
    int Ordem);

public record TreinoResponse(
    Guid TreinoId,
    string Nome,
    ObjetivoTreino Objetivo,
    Guid TreinadorId,
    IReadOnlyList<TreinoExercicioResponse> Exercicios,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? NomeAluno = null);

public static class TreinoResponseExtensions
{
    public static TreinoResponse ToResponse(Treino treino, string? nomeAluno = null) => new(
        treino.Id,
        treino.Nome,
        treino.Objetivo,
        treino.TreinadorId,
        treino.Exercicios.OrderBy(te => te.Ordem).Select(te => new TreinoExercicioResponse(
            te.Id,
            te.ExercicioId,
            te.Exercicio?.Nome ?? string.Empty,
            te.Series.OrderBy(s => s.Ordem).Select(s => new SerieConfigResponse(
                s.Id,
                s.Quantidade,
                s.RepeticoesMin,
                s.RepeticoesMax,
                s.Descricao,
                s.Carga,
                s.Descanso,
                s.Ordem
            )).ToList(),
            te.Ordem
        )).ToList(),
        treino.CreatedAt,
        treino.UpdatedAt,
        nomeAluno);
}
