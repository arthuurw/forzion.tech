using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class TreinoAluno : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid AlunoId { get; private set; }
    public TreinoAlunoStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private TreinoAluno() { }

    public static Result<TreinoAluno> Criar(Guid treinoId, Guid alunoId, DateTime agora)
    {
        if (treinoId == Guid.Empty)
            return Result.Failure<TreinoAluno>(TreinoAlunoErrors.TreinoInvalido);
        if (alunoId == Guid.Empty)
            return Result.Failure<TreinoAluno>(TreinoAlunoErrors.AlunoInvalido);

        var treinoAluno = new TreinoAluno
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            AlunoId = alunoId,
            Status = TreinoAlunoStatus.Ativo,
            CreatedAt = agora
        };

        treinoAluno._domainEvents.Add(new TreinoDisponibilizadoEvent(alunoId, treinoId, treinoAluno.Id, agora));

        return Result.Success(treinoAluno);
    }

    public void AlterarStatus(TreinoAlunoStatus status, DateTime agora)
    {
        Status = status;
        UpdatedAt = agora;
    }
}
