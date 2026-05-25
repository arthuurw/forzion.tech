using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Criar(Guid contaId, string tokenHash, DateTime expiresAt, DateTime agora)
    {
        if (contaId == Guid.Empty)
            throw new DomainException("O identificador da conta é inválido.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("O hash do token é obrigatório.");

        if (expiresAt <= agora)
            throw new DomainException("A data de expiração deve ser futura.");

        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = agora
        };
    }

    public void MarcarComoUsado(DateTime agora)
    {
        if (UsedAt.HasValue)
            throw new DomainException("O token já foi utilizado.");

        UsedAt = agora;
    }
}
