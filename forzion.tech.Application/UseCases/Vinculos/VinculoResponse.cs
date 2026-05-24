using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Vinculos;

public record VinculoResponse(
    Guid VinculoId,
    Guid TreinadorId,
    Guid AlunoId,
    Guid? PacoteId,
    VinculoStatus Status,
    DateTime CreatedAt);
