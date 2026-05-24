namespace forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;

public record AprovarVinculoCommand(Guid VinculoId, Guid TreinadorId, Guid PacoteId, bool TrarFichas = false);
