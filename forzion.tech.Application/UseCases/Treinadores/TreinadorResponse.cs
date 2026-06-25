using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores;

public record TreinadorResponse(
    Guid TreinadorId,
    Guid ContaId,
    string Nome,
    TreinadorStatus Status,
    Guid? PlanoPlataformaId,
    DateTime CreatedAt)
{
    public static TreinadorResponse De(Treinador t) =>
        new(t.Id, t.ContaId, t.Nome, t.Status, t.PlanoPlataformaId, t.CreatedAt);
}
