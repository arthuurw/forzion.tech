using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public Guid PlanoId { get; private set; }
    public Plano Plano { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Tenant() { }

    public static Tenant Criar(string nome, Slug slug, Guid planoId)
    {
        ArgumentNullException.ThrowIfNull(slug);

        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do tenant é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome do tenant deve ter no máximo 100 caracteres.");
        if (planoId == Guid.Empty)
            throw new DomainException("O plano é inválido.");

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            Slug = slug,
            PlanoId = planoId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
