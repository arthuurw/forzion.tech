using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Planos;

public record PlanoPlataformaResponse(
    Guid PlanoId,
    string Nome,
    TierPlano Tier,
    int MaxAlunos,
    decimal Preco,
    bool IsAtivo,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? Descricao = null);

public static class PlanoPlataformaResponseExtensions
{
    public static PlanoPlataformaResponse ToResponse(PlanoPlataforma plano) => new(
        plano.Id, plano.Nome, plano.Tier, plano.MaxAlunos, plano.Preco, plano.IsAtivo, plano.CreatedAt, plano.UpdatedAt, plano.Descricao);
}
