namespace forzion.tech.Application.UseCases.Treinadores.DefinirCortesia;

public record DefinirCortesiaCommand(Guid TreinadorId, Guid? PlanoId, Guid AdminId);
