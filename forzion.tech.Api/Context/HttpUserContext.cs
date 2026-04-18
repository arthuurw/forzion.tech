using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Api.Context;

/// <summary>
/// Implementação de IUserContext que extrai informações do usuário autenticado a partir dos claims JWT.
/// </summary>
public class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// Obtém o identificador da conta do usuário autenticado a partir do claim 'conta_id'.
    /// </summary>
    public Guid ContaId => ParseClaim("conta_id");

    /// <summary>
    /// Obtém o tipo de conta do usuário autenticado a partir do claim 'tipo_conta'.
    /// </summary>
    public TipoConta TipoConta => Enum.TryParse<TipoConta>(Claim("tipo_conta"), out var t) ? t : TipoConta.Aluno;

    /// <summary>
    /// Obtém o identificador do perfil (SystemUser.Id, Treinador.Id ou Aluno.Id) a partir do claim 'perfil_id'.
    /// </summary>
    public Guid PerfilId => ParseClaim("perfil_id");

    /// <summary>
    /// Extrai um claim do token JWT do usuário autenticado.
    /// </summary>
    /// <param name="type">O tipo de claim a extrair.</param>
    /// <returns>O valor do claim, ou nulo se o claim não existir ou o usuário não estiver autenticado.</returns>
    private string? Claim(string type) =>
        _httpContextAccessor.HttpContext?.User.FindFirst(type)?.Value;

    /// <summary>
    /// Extrai e converte um claim do token JWT para um GUID.
    /// </summary>
    /// <param name="type">O tipo de claim a extrair.</param>
    /// <returns>O GUID extraído, ou Guid.Empty se a conversão falhar.</returns>
    private Guid ParseClaim(string type) =>
        Guid.TryParse(Claim(type), out var id) ? id : Guid.Empty;
}
