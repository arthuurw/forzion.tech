using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class SystemUser
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public SystemRole Role { get; private set; }
    public UsuarioStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private SystemUser() { }

    public static SystemUser Criar(Guid contaId, string nome, SystemRole role = SystemRole.SuperAdmin)
    {
        if (contaId == Guid.Empty)
            throw new DomainException("O identificador da conta é inválido.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");

        return new SystemUser
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Nome = nome.Trim(),
            Role = role,
            Status = UsuarioStatus.Ativo,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AlterarRole(SystemRole novoRole)
    {
        Role = novoRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AlterarStatus(UsuarioStatus novoStatus)
    {
        Status = novoStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
