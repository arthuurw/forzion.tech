using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class TokenRevogado
{
    public Guid Jti { get; private set; }
    public DateTime ExpiraEm { get; private set; }

    private TokenRevogado() { }

    public static Result<TokenRevogado> Criar(Guid jti, DateTime expiraEm, DateTime agora)
    {
        if (jti == Guid.Empty)
            return Result.Failure<TokenRevogado>(TokenErrors.JtiInvalido);
        if (expiraEm <= agora)
            return Result.Failure<TokenRevogado>(TokenErrors.ExpiracaoNaoFuturaRevogado);

        return Result.Success(new TokenRevogado { Jti = jti, ExpiraEm = expiraEm });
    }
}
