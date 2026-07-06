using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores;

public record TreinadorResponse(
    Guid TreinadorId,
    Guid ContaId,
    string Nome,
    TreinadorStatus Status,
    Guid? PlanoPlataformaId,
    DateTime CreatedAt,
    Guid? PlanoCortesiaId = null)
{
    public static TreinadorResponse De(Treinador t) =>
        new(t.Id, t.ContaId, t.Nome, t.Status, t.PlanoPlataformaId, t.CreatedAt, t.PlanoCortesiaId);
}
