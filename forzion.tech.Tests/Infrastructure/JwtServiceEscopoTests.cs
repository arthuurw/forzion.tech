using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using forzion.tech.Application.Auth;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Infrastructure;

public class JwtServiceEscopoTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    private static JwtService CriarServico(TimeProvider time) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = "forzion-test-secret-key-32-bytes!!",
                ["Auth:JwtIssuer"] = "forzion.tech",
                ["Auth:JwtAudience"] = "forzion.tech",
            })
            .Build(), time);

    private static Conta Conta() =>
        forzion.tech.Domain.Entities.Conta.Criar(Email.Criar("user@test.com").Value, "hash", TipoConta.Aluno, Agora).Value;

    [Fact]
    public void GerarTokenEscopo_PortaScopeJtiExpCurto_SemClaimsDeNegocio()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(Agora));
        var conta = Conta();

        var emitido = CriarServico(time).GerarTokenEscopo(conta, MfaScopes.Pendente, TimeSpan.FromMinutes(5));

        emitido.ExpiraEm.Should().Be(Agora.AddMinutes(5));
        emitido.Jti.Should().NotBe(Guid.Empty);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(emitido.Token);
        jwt.Claims.Should().Contain(c => c.Type == "scope" && c.Value == MfaScopes.Pendente);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == conta.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "jti" && c.Value == emitido.Jti.ToString());
        jwt.Claims.Should().NotContain(c => c.Type == "tipo_conta");
        jwt.Claims.Should().NotContain(c => c.Type == "perfil_id");
    }

    [Fact]
    public void GerarTokenEscopo_StepUp_CarregaEscopoStepUp()
    {
        var emitido = CriarServico(new FakeTimeProvider(new DateTimeOffset(Agora)))
            .GerarTokenEscopo(Conta(), MfaScopes.StepUp, TimeSpan.FromMinutes(5));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(emitido.Token);
        jwt.Claims.Should().Contain(c => c.Type == "scope" && c.Value == MfaScopes.StepUp);
    }
}
