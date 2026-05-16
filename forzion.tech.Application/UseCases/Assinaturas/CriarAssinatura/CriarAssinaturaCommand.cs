namespace forzion.tech.Application.UseCases.Assinaturas.CriarAssinatura;

public record CriarAssinaturaCommand(
    Guid VinculoId,
    Guid PacoteAlunoId,
    Guid TreinadorId,
    Guid AlunoId,
    decimal Valor);
