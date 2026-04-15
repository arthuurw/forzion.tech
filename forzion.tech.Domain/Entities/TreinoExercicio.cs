using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class TreinoExercicio
{
    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid ExercicioId { get; private set; }
    public int Series { get; private set; }
    public int Repeticoes { get; private set; }
    public decimal? Carga { get; private set; }
    public int? Descanso { get; private set; }
    public int Ordem { get; private set; }

    public Exercicio Exercicio { get; private set; } = null!;

    private TreinoExercicio() { }

    internal void AlterarOrdem(int ordem) => Ordem = ordem;

    internal static TreinoExercicio Criar(
        Guid treinoId, Guid exercicioId, int series, int repeticoes,
        decimal? carga, int? descanso, int ordem)
    {
        if (treinoId == Guid.Empty)
            throw new DomainException("O treino é inválido.");
        if (exercicioId == Guid.Empty)
            throw new DomainException("O exercício é inválido.");
        if (series < 1)
            throw new DomainException("O número de séries deve ser maior que zero.");
        if (repeticoes < 1)
            throw new DomainException("O número de repetições deve ser maior que zero.");
        if (carga is not null && carga < 0)
            throw new DomainException("A carga não pode ser negativa.");
        if (descanso is not null && descanso < 0)
            throw new DomainException("O descanso não pode ser negativo.");

        return new TreinoExercicio
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            ExercicioId = exercicioId,
            Series = series,
            Repeticoes = repeticoes,
            Carga = carga,
            Descanso = descanso,
            Ordem = ordem
        };
    }
}
