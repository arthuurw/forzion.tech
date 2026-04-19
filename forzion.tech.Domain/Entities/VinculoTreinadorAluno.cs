using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class VinculoTreinadorAluno
{
    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public Guid AlunoId { get; private set; }
    public Guid? PacoteAlunoId { get; private set; }
    public VinculoStatus Status { get; private set; }
    public Guid? AprovadoPorId { get; private set; }
    public DateTime? AprovadoEm { get; private set; }
    public DateTime? DataInicio { get; private set; }
    public DateTime? DataFim { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private VinculoTreinadorAluno() { }

    public static VinculoTreinadorAluno Criar(Guid treinadorId, Guid alunoId, Guid? pacoteAlunoId = null)
    {
        if (treinadorId == Guid.Empty)
            throw new DomainException("O identificador do treinador é inválido.");
        if (alunoId == Guid.Empty)
            throw new DomainException("O identificador do aluno é inválido.");
        if (pacoteAlunoId.HasValue && pacoteAlunoId.Value == Guid.Empty)
            throw new DomainException("O identificador do pacote é inválido.");

        return new VinculoTreinadorAluno
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            AlunoId = alunoId,
            PacoteAlunoId = pacoteAlunoId,
            Status = VinculoStatus.AguardandoAprovacao,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Aprovar(Guid aprovadoPorId, Guid pacoteAlunoId)
    {
        if (Status != VinculoStatus.AguardandoAprovacao)
            throw new DomainException("Apenas vínculos aguardando aprovação podem ser aprovados.");
        if (pacoteAlunoId == Guid.Empty)
            throw new DomainException("O identificador do pacote é inválido.");

        Status = VinculoStatus.Ativo;
        PacoteAlunoId = pacoteAlunoId;
        AprovadoPorId = aprovadoPorId;
        AprovadoEm = DateTime.UtcNow;
        DataInicio = DateTime.UtcNow;
    }

    public void Inativar()
    {
        if (Status == VinculoStatus.Inativo)
            throw new DomainException("O vínculo já está inativo.");

        Status = VinculoStatus.Inativo;
        DataFim = DateTime.UtcNow;
    }
}
