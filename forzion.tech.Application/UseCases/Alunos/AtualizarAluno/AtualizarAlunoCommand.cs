namespace forzion.tech.Application.UseCases.Alunos.AtualizarAluno;

public record AtualizarAlunoCommand(
    Guid TenantId,
    Guid AlunoId,
    string? Nome,
    string? Email,
    string? Telefone
);
