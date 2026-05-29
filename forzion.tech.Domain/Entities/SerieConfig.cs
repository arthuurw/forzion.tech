using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class SerieConfig
{
    public Guid Id { get; private set; }
    public Guid TreinoExercicioId { get; private set; }
    public int Quantidade { get; private set; }
    public int RepeticoesMin { get; private set; }
    public int? RepeticoesMax { get; private set; }
    public string? Descricao { get; private set; }
    public decimal? Carga { get; private set; }
    public int? Descanso { get; private set; }
    public int Ordem { get; private set; }

    private SerieConfig() { }

    internal static Result<SerieConfig> Criar(
        Guid treinoExercicioId,
        int quantidade,
        int repeticoesMin,
        int? repeticoesMax,
        string? descricao,
        decimal? carga,
        int? descanso,
        int ordem)
    {
        if (quantidade < 1)
            return Result.Failure<SerieConfig>(TreinoErrors.QuantidadeInvalida);
        if (repeticoesMin < 1)
            return Result.Failure<SerieConfig>(TreinoErrors.RepeticoesMinInvalida);
        if (repeticoesMax.HasValue && repeticoesMax.Value < repeticoesMin)
            return Result.Failure<SerieConfig>(TreinoErrors.RepeticoesMaxMenorQueMin);
        if (carga is not null && carga < 0)
            return Result.Failure<SerieConfig>(TreinoErrors.CargaNegativa);
        if (descanso is not null && descanso < 0)
            return Result.Failure<SerieConfig>(TreinoErrors.DescansoNegativo);

        return Result.Success(new SerieConfig
        {
            Id = Guid.NewGuid(),
            TreinoExercicioId = treinoExercicioId,
            Quantidade = quantidade,
            RepeticoesMin = repeticoesMin,
            RepeticoesMax = repeticoesMax,
            Descricao = descricao?.Trim() is { Length: > 0 } d ? d : null,
            Carga = carga,
            Descanso = descanso,
            Ordem = ordem
        });
    }
}
