using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Conta
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public TipoConta TipoConta { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Conta() { }

    public static Conta Criar(Email email, string passwordHash, TipoConta tipoConta)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("O hash da senha é obrigatório.");

        return new Conta
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            TipoConta = tipoConta,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AtualizarSenha(string novoHash)
    {
        if (string.IsNullOrWhiteSpace(novoHash))
            throw new DomainException("O hash da senha é obrigatório.");

        PasswordHash = novoHash;
        UpdatedAt = DateTime.UtcNow;
    }
}
