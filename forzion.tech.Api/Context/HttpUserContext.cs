using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Api.Context;

// TODO: implementar extração real dos claims JWT em T4 (middleware de autenticação)
public class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Guid ContaId => ParseClaim("conta_id");
    public TipoConta TipoConta => Enum.TryParse<TipoConta>(Claim("tipo_conta"), out var t) ? t : TipoConta.Aluno;
    public Guid PerfilId => ParseClaim("perfil_id");

    private string? Claim(string type) =>
        _httpContextAccessor.HttpContext?.User.FindFirst(type)?.Value;

    private Guid ParseClaim(string type) =>
        Guid.TryParse(Claim(type), out var id) ? id : Guid.Empty;
}
