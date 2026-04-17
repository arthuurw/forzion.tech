namespace forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;

public record AprovarTreinadorCommand(Guid TreinadorId, Guid AdminId, string? Observacao = null);
