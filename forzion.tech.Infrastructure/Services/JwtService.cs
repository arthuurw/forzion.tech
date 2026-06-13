using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace forzion.tech.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public JwtService(IConfiguration configuration, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _configuration = configuration;
        _timeProvider = timeProvider;

        _secret = configuration["Auth:JwtSecret"]
            ?? throw new InvalidOperationException("Configuração 'Auth:JwtSecret' não encontrada.");

        if (System.Text.Encoding.UTF8.GetByteCount(_secret) < 32)
            throw new InvalidOperationException("Auth:JwtSecret deve ter ao menos 32 bytes (256 bits).");
        _issuer = configuration["Auth:JwtIssuer"] ?? "forzion.tech";
        _audience = configuration["Auth:JwtAudience"] ?? "forzion.tech";
    }

    public string GerarToken(Conta conta, Guid perfilId, string nome, Guid familiaId = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            // sub = identidade estável p/ particionar o rate-limit por usuário (KeyFromIpOrSub).
            new(JwtRegisteredClaimNames.Sub, conta.Id.ToString()),
            new("conta_id", conta.Id.ToString()),
            new("tipo_conta", conta.TipoConta.ToString()),
            new("perfil_id", perfilId.ToString()),
            new("nome", nome),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // fam = família de refresh desta sessão; o logout revoga só este device por ela.
        if (familiaId != Guid.Empty)
            claims.Add(new Claim("fam", familiaId.ToString()));

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        // Access curto por papel (NR-4/NR-7): janela menor encurta o roubo do access; a
        // blacklist jti continua eficaz. Refresh (rotação) repõe o access em silêncio.
        var expiration = TimeSpan.FromMinutes(SessaoConfig.AccessMinutes(_configuration, conta.TipoConta));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: agora,
            expires: agora.Add(expiration),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
