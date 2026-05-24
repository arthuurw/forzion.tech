using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.UseCases.Pacotes;

public record PacoteResponse(
    Guid PacoteId,
    Guid TreinadorId,
    string Nome,
    string? Descricao,
    decimal Preco,
    bool IsAtivo,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public static class PacoteResponseExtensions
{
    public static PacoteResponse ToResponse(Pacote pacote) => new(
        pacote.Id, pacote.TreinadorId, pacote.Nome, pacote.Descricao, pacote.Preco, pacote.IsAtivo, pacote.CreatedAt, pacote.UpdatedAt);
}
