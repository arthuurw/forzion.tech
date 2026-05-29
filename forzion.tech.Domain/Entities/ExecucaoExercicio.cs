using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class ExecucaoExercicio
{
    public Guid Id { get; private set; }
    public Guid ExecucaoTreinoId { get; private set; }
    public Guid TreinoExercicioId { get; private set; }
    public int SeriesExecutadas { get; private set; }
    public int RepeticoesExecutadas { get; private set; }
    public decimal? CargaExecutada { get; private set; }
    public string? Observacao { get; private set; }

    private ExecucaoExercicio() { }

    internal static Result<ExecucaoExercicio> Criar(
        Guid execucaoTreinoId,
        Guid treinoExercicioId,
        int seriesExecutadas,
        int repeticoesExecutadas,
        decimal? cargaExecutada,
        string? observacao)
    {
        if (execucaoTreinoId == Guid.Empty)
            return Result.Failure<ExecucaoExercicio>(ExecucaoTreinoErrors.ExecucaoInvalida);
        if (treinoExercicioId == Guid.Empty)
            return Result.Failure<ExecucaoExercicio>(ExecucaoTreinoErrors.ExercicioTreinoInvalido);
        if (seriesExecutadas < 1)
            return Result.Failure<ExecucaoExercicio>(ExecucaoTreinoErrors.SeriesInvalidas);
        if (repeticoesExecutadas < 1)
            return Result.Failure<ExecucaoExercicio>(ExecucaoTreinoErrors.RepeticoesInvalidas);
        if (cargaExecutada is not null && cargaExecutada < 0)
            return Result.Failure<ExecucaoExercicio>(ExecucaoTreinoErrors.CargaNegativa);
        if (observacao is not null && observacao.Length > 500)
            return Result.Failure<ExecucaoExercicio>(ExecucaoTreinoErrors.ExercicioObservacaoMuitoLonga);

        return Result.Success(new ExecucaoExercicio
        {
            Id = Guid.NewGuid(),
            ExecucaoTreinoId = execucaoTreinoId,
            TreinoExercicioId = treinoExercicioId,
            SeriesExecutadas = seriesExecutadas,
            RepeticoesExecutadas = repeticoesExecutadas,
            CargaExecutada = cargaExecutada,
            Observacao = observacao
        });
    }
}
