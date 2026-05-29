using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class VinculoTreinadorAluno : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public Guid AlunoId { get; private set; }
    public Guid? PacoteId { get; private set; }
    public VinculoStatus Status { get; private set; }
    public Guid? AprovadoPorId { get; private set; }
    public DateTime? AprovadoEm { get; private set; }
    public DateTime? DataInicio { get; private set; }
    public DateTime? DataFim { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private VinculoTreinadorAluno() { }

    public static Result<VinculoTreinadorAluno> Criar(Guid treinadorId, Guid alunoId, DateTime agora, Guid? pacoteId = null)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<VinculoTreinadorAluno>(VinculoErrors.TreinadorIdInvalido);
        if (alunoId == Guid.Empty)
            return Result.Failure<VinculoTreinadorAluno>(VinculoErrors.AlunoIdInvalido);
        if (pacoteId.HasValue && pacoteId.Value == Guid.Empty)
            return Result.Failure<VinculoTreinadorAluno>(VinculoErrors.PacoteIdInvalido);

        return Result.Success(new VinculoTreinadorAluno
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            AlunoId = alunoId,
            PacoteId = pacoteId,
            Status = VinculoStatus.AguardandoAprovacao,
            CreatedAt = agora
        });
    }

    public Result Aprovar(Guid aprovadoPorId, Guid pacoteId, DateTime agora)
    {
        if (Status != VinculoStatus.AguardandoAprovacao)
            return Result.Failure(VinculoErrors.NaoAguardandoAprovacao);
        if (pacoteId == Guid.Empty)
            return Result.Failure(VinculoErrors.PacoteIdInvalido);

        Status = VinculoStatus.Ativo;
        PacoteId = pacoteId;
        AprovadoPorId = aprovadoPorId;
        AprovadoEm = agora;
        DataInicio = agora;
        _domainEvents.Add(new VinculoAprovadoEvent(Id, TreinadorId, AlunoId, aprovadoPorId, agora));
        return Result.Success();
    }

    public Result Inativar(DateTime agora)
    {
        if (Status == VinculoStatus.Inativo)
            return Result.Failure(VinculoErrors.JaInativo);

        Status = VinculoStatus.Inativo;
        DataFim = agora;
        return Result.Success();
    }
}
