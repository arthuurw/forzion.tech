using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;

public record AlterarStatusAlunoCommand(
    Guid AlunoId,
    AlunoStatus NovoStatus
);
