using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Planos;

public record PlanoTreinadorResponse(
    Guid PlanoId,
    string Nome,
    TierPlano Tier,
    int MaxAlunos,
    decimal Preco,
    bool IsAtivo,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public static class PlanoTreinadorResponseExtensions
{
    public static PlanoTreinadorResponse ToResponse(PlanoTreinador plano) => new(
        plano.Id, plano.Nome, plano.Tier, plano.MaxAlunos, plano.Preco, plano.IsAtivo, plano.CreatedAt, plano.UpdatedAt);
}
