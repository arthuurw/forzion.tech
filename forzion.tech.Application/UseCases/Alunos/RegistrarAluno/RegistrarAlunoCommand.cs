namespace forzion.tech.Application.UseCases.Alunos.RegistrarAluno;

public record RegistrarAlunoCommand(
    string Email,
    string Senha,
    string Nome,
    Guid TreinadorId,
    string? Telefone = null);
