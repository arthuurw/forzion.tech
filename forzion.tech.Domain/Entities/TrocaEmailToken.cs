using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class TrocaEmailToken
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string NovoEmail { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiraEm { get; private set; }
    public DateTime? UsadoEm { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private TrocaEmailToken() { }

    public static Result<TrocaEmailToken> Criar(Guid contaId, string novoEmail, string tokenHash, DateTime expiraEm, DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<TrocaEmailToken>(TokenErrors.ContaIdInvalido);

        if (string.IsNullOrWhiteSpace(novoEmail))
            return Result.Failure<TrocaEmailToken>(Error.Validation("troca_email.novo_email_obrigatorio", "O novo e-mail é obrigatório."));

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<TrocaEmailToken>(TokenErrors.TokenHashObrigatorio);

        if (expiraEm <= agora)
            return Result.Failure<TrocaEmailToken>(TokenErrors.ExpiracaoNaoFutura);

        return Result.Success(new TrocaEmailToken
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            NovoEmail = novoEmail,
            TokenHash = tokenHash,
            ExpiraEm = expiraEm,
            CriadoEm = agora
        });
    }

    public Result MarcarComoUsado(DateTime agora)
    {
        if (UsadoEm.HasValue)
            return Result.Failure(TokenErrors.JaUtilizado);

        UsadoEm = agora;
        return Result.Success();
    }
}
