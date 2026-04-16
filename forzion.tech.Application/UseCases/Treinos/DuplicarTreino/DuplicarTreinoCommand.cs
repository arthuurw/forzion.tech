namespace forzion.tech.Application.UseCases.Treinos.DuplicarTreino;

public record DuplicarTreinoCommand(Guid TenantId, Guid TreinadorId, Guid TreinoId);
