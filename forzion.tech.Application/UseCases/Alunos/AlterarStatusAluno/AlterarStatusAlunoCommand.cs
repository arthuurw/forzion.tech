using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;

public record AlterarStatusAlunoCommand(
    Guid TenantId,
    Guid AdminId,
    Guid AlunoId,
    AlunoStatus NovoStatus
);
