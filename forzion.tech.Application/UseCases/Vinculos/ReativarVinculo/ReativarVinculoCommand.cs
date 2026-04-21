namespace forzion.tech.Application.UseCases.Vinculos.ReativarVinculo;

public record ReativarVinculoCommand(Guid TreinadorId, Guid AlunoId, Guid PacoteAlunoId);
