using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

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

    public static Result<TreinoAluno> Criar(Guid treinoId, Guid alunoId, DateTime agora)
    {
        if (treinoId == Guid.Empty)
            return Result.Failure<TreinoAluno>(TreinoAlunoErrors.TreinoInvalido);
        if (alunoId == Guid.Empty)
            return Result.Failure<TreinoAluno>(TreinoAlunoErrors.AlunoInvalido);

        return Result.Success(new TreinoAluno
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            AlunoId = alunoId,
            Status = TreinoAlunoStatus.Ativo,
            CreatedAt = agora
        });
    }

    public void AlterarStatus(TreinoAlunoStatus status, DateTime agora)
    {
        Status = status;
        UpdatedAt = agora;
    }
}
