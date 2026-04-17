namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

public record CadastrarAlunoCommand(
    Guid ContaId,
    string Nome,
    string? Email,
    string? Telefone
);
