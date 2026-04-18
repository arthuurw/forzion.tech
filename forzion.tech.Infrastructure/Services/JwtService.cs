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
    private readonly TimeSpan _expiration;

    public JwtService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _secret = configuration["Auth:JwtSecret"]
            ?? throw new InvalidOperationException("Configuração 'Auth:JwtSecret' não encontrada.");
        _issuer = configuration["Auth:JwtIssuer"] ?? "forzion.tech";
        _audience = configuration["Auth:JwtAudience"] ?? "forzion.tech";

        var expirationMinutes = int.TryParse(configuration["Auth:JwtExpirationMinutes"], out var m) ? m : 60;
        _expiration = TimeSpan.FromMinutes(expirationMinutes);
    }

    public string GerarToken(Conta conta, Guid perfilId)
    {
        ArgumentNullException.ThrowIfNull(conta);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("conta_id", conta.Id.ToString()),
            new Claim("tipo_conta", conta.TipoConta.ToString()),
            new Claim("perfil_id", perfilId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(_expiration),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
