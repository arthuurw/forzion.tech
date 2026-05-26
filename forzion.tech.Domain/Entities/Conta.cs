using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Conta : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public TipoConta TipoConta { get; private set; }
    public bool EmailVerificado { get; private set; }
    public DateTime? VerificadoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Conta() { }

    public static Conta Criar(Email email, string passwordHash, TipoConta tipoConta, DateTime agora)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("O hash da senha é obrigatório.");

        var conta = new Conta
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            TipoConta = tipoConta,
            EmailVerificado = false,
            CreatedAt = agora
        };

        conta._domainEvents.Add(new ContaRegistradaEvent(conta.Id, email.Value, agora));

        return conta;
    }

    public void AtualizarSenha(string novoHash)
    {
        if (string.IsNullOrWhiteSpace(novoHash))
            throw new DomainException("O hash da senha é obrigatório.");

        PasswordHash = novoHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarcarEmailVerificado(DateTime agora)
    {
        if (EmailVerificado) return;

        EmailVerificado = true;
        VerificadoEm = agora;
        UpdatedAt = agora;
    }
}
