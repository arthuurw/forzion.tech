using forzion.tech.Domain.Exceptions;

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

    internal static SerieConfig Criar(
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
            throw new DomainException("A quantidade de séries deve ser maior que zero.");
        if (repeticoesMin < 1)
            throw new DomainException("O número mínimo de repetições deve ser maior que zero.");
        if (repeticoesMax.HasValue && repeticoesMax.Value < repeticoesMin)
            throw new DomainException("O máximo de repetições não pode ser menor que o mínimo.");
        if (carga is not null && carga < 0)
            throw new DomainException("A carga não pode ser negativa.");
        if (descanso is not null && descanso < 0)
            throw new DomainException("O descanso não pode ser negativo.");

        return new SerieConfig
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
        };
    }
}
