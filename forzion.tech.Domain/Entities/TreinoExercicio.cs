using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class TreinoExercicio
{
    private readonly List<SerieConfig> _series = [];

    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid ExercicioId { get; private set; }
    public int Ordem { get; private set; }

    public Exercicio Exercicio { get; private set; } = null!;
    public IReadOnlyList<SerieConfig> Series => _series.AsReadOnly();

    private TreinoExercicio() { }

    internal void AlterarOrdem(int ordem) => Ordem = ordem;

    public void AdicionarSerie(
        int quantidade, int repeticoesMin, int? repeticoesMax,
        string? descricao, decimal? carga, int? descanso)
    {
        var ordem = _series.Count + 1;
        _series.Add(SerieConfig.Criar(Id, quantidade, repeticoesMin, repeticoesMax, descricao, carga, descanso, ordem));
    }

    internal static TreinoExercicio Criar(Guid treinoId, Guid exercicioId, int ordem)
    {
        if (treinoId == Guid.Empty)
            throw new DomainException("O treino é inválido.");
        if (exercicioId == Guid.Empty)
            throw new DomainException("O exercício é inválido.");

        return new TreinoExercicio
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            ExercicioId = exercicioId,
            Ordem = ordem
        };
    }
}
