namespace forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;

public record ExcluirTreinadorCommand(Guid TreinadorId, Guid AdminId);
