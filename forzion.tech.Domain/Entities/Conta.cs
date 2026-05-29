using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
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

    public static Result<Conta> Criar(Email email, string passwordHash, TipoConta tipoConta, DateTime agora)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<Conta>(ContaErrors.PasswordHashObrigatorio);

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

        return Result.Success(conta);
    }

    public Result AtualizarSenha(string novoHash, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(novoHash))
            return Result.Failure(ContaErrors.PasswordHashObrigatorio);

        PasswordHash = novoHash;
        UpdatedAt = agora;
        return Result.Success();
    }

    public void MarcarEmailVerificado(DateTime agora)
    {
        if (EmailVerificado) return;

        EmailVerificado = true;
        VerificadoEm = agora;
        UpdatedAt = agora;
    }
}
