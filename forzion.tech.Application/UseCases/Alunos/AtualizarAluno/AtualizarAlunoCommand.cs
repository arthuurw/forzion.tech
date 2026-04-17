namespace forzion.tech.Application.UseCases.Alunos.AtualizarAluno;

public record AtualizarAlunoCommand(
    Guid AlunoId,
    string? Nome,
    string? Email,
    string? Telefone
);
