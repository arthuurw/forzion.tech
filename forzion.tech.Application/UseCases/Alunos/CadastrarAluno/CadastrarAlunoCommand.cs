namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

public record CadastrarAlunoCommand(
    Guid TenantId,
    Guid TreinadorId,
    string Nome,
    string? Email,
    string? Telefone
);
