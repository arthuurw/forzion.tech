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
    public DateTime? AnonimizadaEm { get; private set; }
    public bool NotificacoesEngajamentoEmailOptOut { get; }

    // epoch de sessão. Access token com nbf anterior a este carimbo é rejeitado.
    // null = nunca invalidado (tokens vigentes valem). Bump em reset/troca de senha/logout-all/anonimização.
    public DateTimeOffset? SessoesInvalidasAntesDeUtc { get; private set; }

    private Conta() { }

    public static Result<Conta> Criar(Email email, string passwordHash, TipoConta tipoConta, DateTime agora, bool emitirRegistro = true)
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

        // Treinador de plano pago: verificação só é enviada após o pagamento (emite o evento depois).
        if (emitirRegistro)
            conta._domainEvents.Add(new ContaRegistradaEvent(conta.Id, email.Value, agora));

        return Result.Success(conta);
    }

    public void EmitirRegistro(DateTime agora)
    {
        _domainEvents.Add(new ContaRegistradaEvent(Id, Email.Value, agora));
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

    public void InvalidarSessoesAnteriores(DateTimeOffset agora)
    {
        SessoesInvalidasAntesDeUtc = agora;
        UpdatedAt = agora.UtcDateTime;
    }

    public Result AtualizarEmail(ValueObjects.Email novoEmail, DateTime agora)
    {
        ArgumentNullException.ThrowIfNull(novoEmail);

        Email = novoEmail;
        EmailVerificado = true;
        VerificadoEm = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Anonimizar(DateTime agora)
    {
        if (AnonimizadaEm is not null)
            return Result.Success();

        var tokenAnon = $"anon+{Guid.NewGuid():N}@anonimizado.local";
        var emailResult = Email.Criar(tokenAnon);
        // The format satisfies the Email VO regex — failure here would be a bug, so propagate.
        if (emailResult.IsFailure)
            return Result.Failure(emailResult.Error!);

        Email = emailResult.Value;
        PasswordHash = string.Empty;
        EmailVerificado = false;
        VerificadoEm = null;
        AnonimizadaEm = agora;
        UpdatedAt = agora;

        _domainEvents.Add(new ContaAnonimizadaEvent(Id, TipoConta, agora));

        return Result.Success();
    }
}
