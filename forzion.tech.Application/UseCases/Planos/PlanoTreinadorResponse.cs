using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.UseCases.Planos;

public record PlanoTreinadorResponse(
    Guid PlanoId,
    string Nome,
    int MaxAlunos,
    decimal Preco,
    bool IsAtivo,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public static class PlanoTreinadorResponseExtensions
{
    public static PlanoTreinadorResponse ToResponse(PlanoTreinador plano) => new(
        plano.Id, plano.Nome, plano.MaxAlunos, plano.Preco, plano.IsAtivo, plano.CreatedAt, plano.UpdatedAt);
}
