using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.Tests.Infrastructure;

public class JwtServiceTests
{
    private const string Secret = "forzion-test-secret-key-32-bytes!!";
    private const string Issuer = "forzion.tech";
    private const string Audience = "forzion.tech";

    private static JwtService CriarServico(string? secret = Secret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = secret,
                ["Auth:JwtIssuer"] = Issuer,
                ["Auth:JwtAudience"] = Audience,
                ["Auth:JwtExpirationMinutes"] = "60"
            })
            .Build();

        return new JwtService(config);
    }

    [Fact]
    public void GerarToken_ContaValida_RetornaStringNaoVazia()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var perfilId = Guid.NewGuid();

        var token = CriarServico().GerarToken(conta, perfilId);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GerarToken_ContaValida_TokenContemClaimsCorretos()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var perfilId = Guid.NewGuid();

        var token = CriarServico().GerarToken(conta, perfilId);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "conta_id" && c.Value == conta.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "tipo_conta" && c.Value == "Treinador");
        jwt.Claims.Should().Contain(c => c.Type == "perfil_id" && c.Value == perfilId.ToString());
    }

    [Fact]
    public void GerarToken_ContaSystemAdmin_TokenContemTipoCorreto()
    {
        var conta = Conta.Criar(Email.Criar("admin@test.com").Value, "hash", TipoConta.SystemAdmin, DateTime.UtcNow).Value;
        var perfilId = conta.Id;

        var token = CriarServico().GerarToken(conta, perfilId);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "tipo_conta" && c.Value == "SystemAdmin");
    }

    [Fact]
    public void GerarToken_ContaAluno_TokenContemTipoCorreto()
    {
        var conta = Conta.Criar(Email.Criar("aluno@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var perfilId = Guid.NewGuid();

        var token = CriarServico().GerarToken(conta, perfilId);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "tipo_conta" && c.Value == "Aluno");
    }

    [Fact]
    public void GerarToken_ContaNula_LancaArgumentNullException()
    {
        var act = () => CriarServico().GerarToken(null!, Guid.NewGuid());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GerarToken_TokenPossuiIssuerEAudienceCorretos()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;

        var token = CriarServico().GerarToken(conta, Guid.NewGuid());

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
    }

    [Fact]
    public void GerarToken_TokenPossuiExpiracao()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;

        var token = CriarServico().GerarToken(conta, Guid.NewGuid());

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void Construtor_SecretNaoConfigurado_LancaInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtIssuer"] = Issuer
            })
            .Build();

        var act = () => new JwtService(config);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Auth:JwtSecret*");
    }

    [Fact]
    public void Construtor_SecretCurto_LancaInvalidOperationException()
    {
        // Validates byte count (not char count) — a 31-byte ASCII secret must be rejected.
        // "0123456789012345678901234567890" is exactly 31 ASCII characters = 31 UTF-8 bytes.
        var secret = "0123456789012345678901234567890"; // 31 chars / 31 bytes
        secret.Length.Should().Be(31); // guard: ensure the constant is correct
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = secret,
                ["Auth:JwtIssuer"] = Issuer,
                ["Auth:JwtAudience"] = Audience,
            })
            .Build();

        var act = () => new JwtService(config);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void Construtor_SecretCom32Bytes_NaoLancaExcecao()
    {
        // Exactly 32 ASCII chars = 32 UTF-8 bytes — should be accepted.
        var secret = "01234567890123456789012345678901"; // 32 chars / 32 bytes
        secret.Length.Should().Be(32);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = secret,
                ["Auth:JwtIssuer"] = Issuer,
                ["Auth:JwtAudience"] = Audience,
            })
            .Build();

        var act = () => new JwtService(config);
        act.Should().NotThrow();
    }
}
