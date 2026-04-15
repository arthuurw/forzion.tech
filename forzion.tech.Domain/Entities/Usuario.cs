using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Usuario
{
    /// <summary>
    /// Id correspondente ao UUID do usuário no Supabase Auth.
    /// </summary>
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public Role Role { get; private set; }
    public UsuarioStatus Status { get; private set; }
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string? FotoUrl { get; private set; }
    public string? Bio { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Usuario() { }

    public static Usuario Criar(Guid supabaseId, string nome, Email email, Guid tenantId, Role role = Role.Admin)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (supabaseId == Guid.Empty)
            throw new DomainException("O identificador do usuário é inválido.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");
        if (tenantId == Guid.Empty)
            throw new DomainException("O tenant é inválido.");

        return new Usuario
        {
            Id = supabaseId,
            Nome = nome.Trim(),
            Email = email,
            Role = role,
            Status = UsuarioStatus.Ativo,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Atualizar(string? nome, string? fotoUrl, string? bio)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (fotoUrl is not null)
        {
            if (fotoUrl.Length > 0)
            {
                if (fotoUrl.Length > 500)
                    throw new DomainException("A URL da foto deve ter no máximo 500 caracteres.");
                if (!Uri.TryCreate(fotoUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                    throw new DomainException("A URL da foto deve ser uma URL válida (http ou https).");
            }
            FotoUrl = string.IsNullOrWhiteSpace(fotoUrl) ? null : fotoUrl;
        }

        if (bio is not null)
        {
            if (bio.Length > 500)
                throw new DomainException("A bio deve ter no máximo 500 caracteres.");
            Bio = string.IsNullOrWhiteSpace(bio) ? null : bio;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void AlterarStatus(UsuarioStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}
