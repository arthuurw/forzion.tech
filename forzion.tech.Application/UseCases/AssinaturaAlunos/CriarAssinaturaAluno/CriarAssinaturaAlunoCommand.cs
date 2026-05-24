namespace forzion.tech.Application.UseCases.AssinaturaAlunos.CriarAssinaturaAluno;

public record CriarAssinaturaAlunoCommand(
    Guid VinculoId,
    Guid PacoteId,
    Guid TreinadorId,
    Guid AlunoId,
    decimal Valor);
