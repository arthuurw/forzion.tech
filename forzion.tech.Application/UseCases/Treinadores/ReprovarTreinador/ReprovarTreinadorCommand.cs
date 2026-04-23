namespace forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;

public record ReprovarTreinadorCommand(Guid TreinadorId, Guid AdminId, string? Observacao = null);
