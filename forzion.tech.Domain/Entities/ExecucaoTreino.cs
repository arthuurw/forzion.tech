using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class ExecucaoTreino : IHasDomainEvents
{
    private readonly List<ExecucaoExercicio> _exercicios = [];
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid AlunoId { get; private set; }
    public DateTime DataExecucao { get; private set; }
    public string? Observacao { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<ExecucaoExercicio> Exercicios => _exercicios.AsReadOnly();

    private ExecucaoTreino() { }

    public static Result<ExecucaoTreino> Criar(Guid treinoId, Guid alunoId, DateTime dataExecucao, DateTime agora, string? observacao = null, string? idempotencyKey = null)
    {
        if (treinoId == Guid.Empty)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.TreinoInvalido);
        if (alunoId == Guid.Empty)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.AlunoInvalido);
        if (dataExecucao == default)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.DataExecucaoInvalida);
        if (observacao is not null && observacao.Length > 500)
            return Result.Failure<ExecucaoTreino>(ExecucaoTreinoErrors.ObservacaoMuitoLonga);

        var execucao = new ExecucaoTreino
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            AlunoId = alunoId,
            DataExecucao = dataExecucao,
            Observacao = observacao,
            IdempotencyKey = idempotencyKey,
            CreatedAt = agora
        };

        execucao._domainEvents.Add(new ExecucaoRegistradaEvent(alunoId, treinoId, execucao.Id, agora));

        return Result.Success(execucao);
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
