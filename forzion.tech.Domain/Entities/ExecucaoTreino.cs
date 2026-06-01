using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class ExecucaoTreino
{
    private readonly List<ExecucaoExercicio> _exercicios = [];

    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid AlunoId { get; private set; }
    public DateTime DataExecucao { get; private set; }
    public string? Observacao { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<ExecucaoExercicio> Exercicios => _exercicios.AsReadOnly();

    private ExecucaoTreino() { }

    public static Result<ExecucaoTreino> Criar(Guid treinoId, Guid alunoId, DateTime dataExecucao, DateTime agora, string? observacao = null)
    {
        if (treinoId == Guid.Empty)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.TreinoInvalido);
        if (alunoId == Guid.Empty)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.AlunoInvalido);
        if (dataExecucao == default)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.DataExecucaoInvalida);
        if (observacao is not null && observacao.Length > 500)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.ObservacaoMuitoLonga);

        return Result.Success(new ExecucaoTreino
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            AlunoId = alunoId,
            DataExecucao = dataExecucao,
            Observacao = observacao,
            CreatedAt = agora
        });
    }

    public Result AdicionarExercicio(
        Guid treinoExercicioId,
        int seriesExecutadas,
        int repeticoesExecutadas,
        decimal? cargaExecutada,
        string? observacao = null)
    {
        var r = ExecucaoExercicio.Criar(
            Id, treinoExercicioId, seriesExecutadas, repeticoesExecutadas, cargaExecutada, observacao);
        if (r.IsFailure)
            return Result.Failure(r.Error!);
        _exercicios.Add(r.Value);
        return Result.Success();
    }
}
