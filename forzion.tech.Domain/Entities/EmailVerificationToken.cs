using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class EmailVerificationToken
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private EmailVerificationToken() { }

    public static Result<EmailVerificationToken> Criar(Guid contaId, string tokenHash, DateTime expiresAt, DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<EmailVerificationToken>(TokenErrors.ContaIdInvalido);

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<EmailVerificationToken>(TokenErrors.TokenHashObrigatorio);

        if (expiresAt <= agora)
            return Result.Failure<EmailVerificationToken>(TokenErrors.ExpiracaoNaoFutura);

        return Result.Success(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = agora
        });
    }

    public Result MarcarComoVerificado(DateTime agora)
    {
        if (VerifiedAt.HasValue)
            return Result.Failure(TokenErrors.JaUtilizado);

        VerifiedAt = agora;
        return Result.Success();
    }
}
