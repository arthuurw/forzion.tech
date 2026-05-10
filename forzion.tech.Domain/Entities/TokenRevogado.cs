using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class TokenRevogado
{
    public Guid Jti { get; private set; }
    public DateTime ExpiraEm { get; private set; }

    private TokenRevogado() { }

    public static TokenRevogado Criar(Guid jti, DateTime expiraEm)
    {
        if (jti == Guid.Empty)
            throw new DomainException("O identificador do token é inválido.");
        if (expiraEm <= DateTime.UtcNow)
            throw new DomainException("A data de expiração do token deve ser futura.");

        return new() { Jti = jti, ExpiraEm = expiraEm };
    }
}
