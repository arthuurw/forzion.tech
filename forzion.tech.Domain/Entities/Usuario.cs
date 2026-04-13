using forzion.tech.Domain.Enums;

namespace forzion.tech.Domain.Entities;

public class Usuario
{
    /// <summary>
    /// Id correspondente ao UUID do usuário no Supabase Auth.
    /// </summary>
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public Role Role { get; private set; }
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private Usuario() { }

    public static Usuario Criar(Guid supabaseId, string nome, string email, Guid tenantId, Role role = Role.Admin)
    {
        return new Usuario
        {
            Id = supabaseId,
            Nome = nome,
            Email = email,
            Role = role,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
