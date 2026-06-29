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
        // Access curto por papel: janela menor encurta o roubo do access; a
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

    public TokenEscopo GerarTokenEscopo(Conta conta, string escopo, TimeSpan validade)
    {
        ArgumentNullException.ThrowIfNull(conta);
        ArgumentException.ThrowIfNullOrWhiteSpace(escopo);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jti = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, conta.Id.ToString()),
            new("conta_id", conta.Id.ToString()),
            new("scope", escopo),
            new(JwtRegisteredClaimNames.Jti, jti.ToString()),
        };

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var expiraEm = agora.Add(validade);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: agora,
            expires: expiraEm,
            signingCredentials: credentials);

        return new TokenEscopo(new JwtSecurityTokenHandler().WriteToken(token), jti, expiraEm);
    }

    public EscopoValidado? ValidarTokenEscopo(string token, string escopoEsperado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(escopoEsperado);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parametros = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parametros, out _);

            if (!string.Equals(principal.FindFirst("scope")?.Value, escopoEsperado, StringComparison.Ordinal))
                return null;
            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var contaId))
                return null;
            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var jti))
                return null;

            return new EscopoValidado(contaId, jti);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}
