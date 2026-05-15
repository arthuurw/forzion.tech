using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class TreinoExercicio
{
    private readonly List<SerieConfig> _series = [];

    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid ExercicioId { get; private set; }
    public int Ordem { get; private set; }
    public string? Observacao { get; private set; }

    public Exercicio Exercicio { get; private set; } = null!;
    public IReadOnlyList<SerieConfig> Series => _series.AsReadOnly();

    private TreinoExercicio() { }

    internal void AlterarOrdem(int ordem) => Ordem = ordem;

    public void AtualizarObservacao(string? observacao)
    {
        if (observacao is not null && observacao.Trim().Length > 500)
            throw new DomainException("A observação deve ter no máximo 500 caracteres.");
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    public void AdicionarSerie(
        int quantidade, int repeticoesMin, int? repeticoesMax,
        string? descricao, decimal? carga, int? descanso)
    {
        var ordem = _series.Count + 1;
        _series.Add(SerieConfig.Criar(Id, quantidade, repeticoesMin, repeticoesMax, descricao, carga, descanso, ordem));
    }

    public void AtualizarSeries(
        IReadOnlyList<(int Quantidade, int RepeticoesMin, int? RepeticoesMax, string? Descricao, decimal? Carga, int? Descanso)> novasSeries)
    {
        if (novasSeries.Count == 0)
            throw new DomainException("O exercício deve ter pelo menos um grupo de séries.");

        _series.Clear();
        foreach (var s in novasSeries)
            AdicionarSerie(s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso);
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
