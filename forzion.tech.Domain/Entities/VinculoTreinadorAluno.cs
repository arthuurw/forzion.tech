using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;

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

    public static VinculoTreinadorAluno Criar(Guid treinadorId, Guid alunoId, Guid? pacoteId = null)
    {
        if (treinadorId == Guid.Empty)
            throw new DomainException("O identificador do treinador é inválido.");
        if (alunoId == Guid.Empty)
            throw new DomainException("O identificador do aluno é inválido.");
        if (pacoteId.HasValue && pacoteId.Value == Guid.Empty)
            throw new DomainException("O identificador do pacote é inválido.");

        return new VinculoTreinadorAluno
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            AlunoId = alunoId,
            PacoteId = pacoteId,
            Status = VinculoStatus.AguardandoAprovacao,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Aprovar(Guid aprovadoPorId, Guid pacoteId)
    {
        if (Status != VinculoStatus.AguardandoAprovacao)
            throw new DomainException("Apenas vínculos aguardando aprovação podem ser aprovados.");
        if (pacoteId == Guid.Empty)
            throw new DomainException("O identificador do pacote é inválido.");

        Status = VinculoStatus.Ativo;
        PacoteId = pacoteId;
        AprovadoPorId = aprovadoPorId;
        AprovadoEm = DateTime.UtcNow;
        DataInicio = DateTime.UtcNow;
        _domainEvents.Add(new VinculoAprovadoEvent(Id, TreinadorId, AlunoId, aprovadoPorId, DateTime.UtcNow));
    }

    public void Inativar()
    {
        if (Status == VinculoStatus.Inativo)
            throw new DomainException("O vínculo já está inativo.");

        Status = VinculoStatus.Inativo;
        DataFim = DateTime.UtcNow;
    }
}
