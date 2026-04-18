namespace forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;

public record VincularFichaAoAlunoCommand(
    Guid TreinoId,
    Guid AlunoId);
