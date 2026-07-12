namespace forzion.tech.Application.UseCases.Vinculos.DefinirPreservacaoVinculo;

public record DefinirPreservacaoVinculoCommand(Guid VinculoId, Guid TreinadorId, bool Preservar);
