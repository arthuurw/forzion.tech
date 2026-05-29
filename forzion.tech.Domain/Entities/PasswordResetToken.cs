using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

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

    public static Result<PasswordResetToken> Criar(Guid contaId, string tokenHash, DateTime expiresAt, DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<PasswordResetToken>(TokenErrors.ContaIdInvalido);

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<PasswordResetToken>(TokenErrors.TokenHashObrigatorio);

        if (expiresAt <= agora)
            return Result.Failure<PasswordResetToken>(TokenErrors.ExpiracaoNaoFutura);

        return Result.Success(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = agora
        });
    }

    public Result MarcarComoUsado(DateTime agora)
    {
        if (UsedAt.HasValue)
            return Result.Failure(TokenErrors.JaUtilizado);

        UsedAt = agora;
        return Result.Success();
    }
}
