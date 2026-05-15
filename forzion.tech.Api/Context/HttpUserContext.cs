using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Api.Context;

public class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Guid ContaId => ParseClaim("conta_id");

    public TipoConta TipoConta =>
        Enum.TryParse<TipoConta>(Claim("tipo_conta"), out var t)
            ? t
            : throw new AcessoNegadoException();

    public Guid PerfilId => ParseClaim("perfil_id");

    public Guid Jti => ParseClaim("jti");

    public DateTime TokenExpiraEm
    {
        get
        {
            var exp = Claim("exp");
            return long.TryParse(exp, out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                : DateTime.MinValue;
        }
    }

    private string? Claim(string type) =>
        _httpContextAccessor.HttpContext?.User.FindFirst(type)?.Value;

    private Guid ParseClaim(string type) =>
        Guid.TryParse(Claim(type), out var id)
            ? id
            : throw new InvalidOperationException($"Claim '{type}' ausente ou inválido no token.");
}
