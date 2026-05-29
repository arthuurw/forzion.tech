using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class TreinoExercicio
{
    private readonly List<SerieConfig> _series = [];

    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid ExercicioId { get; private set; }
    public int Ordem { get; private set; }
    public string? Observacao { get; private set; }

    public IReadOnlyList<SerieConfig> Series => _series.AsReadOnly();

    private TreinoExercicio() { }

    internal void AlterarOrdem(int ordem) => Ordem = ordem;

    public Result AtualizarObservacao(string? observacao)
    {
        if (observacao is not null && observacao.Trim().Length > 500)
            return Result.Failure(TreinoErrors.ObservacaoMuitoLonga);
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        return Result.Success();
    }

    public Result AdicionarSerie(
        int quantidade, int repeticoesMin, int? repeticoesMax,
        string? descricao, decimal? carga, int? descanso)
    {
        var ordem = _series.Count + 1;
        var serieRes = SerieConfig.Criar(Id, quantidade, repeticoesMin, repeticoesMax, descricao, carga, descanso, ordem);
        if (serieRes.IsFailure)
            return Result.Failure(serieRes.Error!);
        _series.Add(serieRes.Value);
        return Result.Success();
    }

    public Result AtualizarSeries(
        IReadOnlyList<(int Quantidade, int RepeticoesMin, int? RepeticoesMax, string? Descricao, decimal? Carga, int? Descanso)> novasSeries)
    {
        if (novasSeries.Count == 0)
            return Result.Failure(TreinoErrors.PeloMenosUmGrupoSeries);

        // Valida e materializa todas as séries antes de mutar o estado.
        var construidas = new List<SerieConfig>(novasSeries.Count);
        var ordem = 1;
        foreach (var s in novasSeries)
        {
            var serieRes = SerieConfig.Criar(Id, s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso, ordem);
            if (serieRes.IsFailure)
                return Result.Failure(serieRes.Error!);
            construidas.Add(serieRes.Value);
            ordem++;
        }

        _series.Clear();
        _series.AddRange(construidas);
        return Result.Success();
    }

    internal static Result<TreinoExercicio> Criar(Guid treinoId, Guid exercicioId, int ordem)
    {
        if (treinoId == Guid.Empty)
            return Result.Failure<TreinoExercicio>(TreinoErrors.TreinoInvalido);
        if (exercicioId == Guid.Empty)
            return Result.Failure<TreinoExercicio>(TreinoErrors.ExercicioInvalido);

        return Result.Success(new TreinoExercicio
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            ExercicioId = exercicioId,
            Ordem = ordem
        });
    }
}
