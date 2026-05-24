using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores;

public record TreinadorResponse(
    Guid TreinadorId,
    Guid ContaId,
    string Nome,
    TreinadorStatus Status,
    Guid? PlanoPlataformaId,
    DateTime CreatedAt);
