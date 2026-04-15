using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class TreinoAluno
{
    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid AlunoId { get; private set; }
    public TreinoAlunoStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private TreinoAluno() { }

    public static TreinoAluno Criar(Guid treinoId, Guid alunoId)
    {
        if (treinoId == Guid.Empty)
            throw new DomainException("O treino é inválido.");
        if (alunoId == Guid.Empty)
            throw new DomainException("O aluno é inválido.");

        return new TreinoAluno
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            AlunoId = alunoId,
            Status = TreinoAlunoStatus.Ativo,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AlterarStatus(TreinoAlunoStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}
