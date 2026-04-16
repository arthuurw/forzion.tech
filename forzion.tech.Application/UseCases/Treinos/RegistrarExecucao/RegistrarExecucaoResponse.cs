namespace forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;

public record RegistrarExecucaoResponse(
    Guid ExecucaoId,
    Guid TreinoId,
    Guid AlunoId,
    DateTime DataExecucao,
    string? Observacao,
    DateTime CreatedAt);
