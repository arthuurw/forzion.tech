namespace forzion.tech.Application.UseCases.Treinadores.InativarTreinador;

public record InativarTreinadorCommand(Guid TreinadorId, Guid AdminId, string? Observacao = null);
