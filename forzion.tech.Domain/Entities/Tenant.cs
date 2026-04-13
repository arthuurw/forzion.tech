namespace forzion.tech.Domain.Entities;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid PlanoId { get; private set; }
    public Plano Plano { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private Tenant() { }

    public static Tenant Criar(string nome, string slug, Guid planoId)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Slug = slug,
            PlanoId = planoId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
