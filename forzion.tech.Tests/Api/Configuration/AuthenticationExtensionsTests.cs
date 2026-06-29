using FluentAssertions;
using forzion.tech.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace forzion.tech.Tests.Api.Configuration;

public class AuthenticationExtensionsTests
{
    private static IWebHostEnvironment CriarEnv(string name = "Development") =>
        Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == name);

    private static IConfiguration CriarConfig(string? secret = null, string? issuer = null, string? audience = null)
    {
        var dict = new Dictionary<string, string?>();
        if (secret is not null) dict["Auth:JwtSecret"] = secret;
        if (issuer is not null) dict["Auth:JwtIssuer"] = issuer;
        if (audience is not null) dict["Auth:JwtAudience"] = audience;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void AddJwtAuthentication_SecretVazio_LancaInvalidOperationException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddJwtAuthentication(CriarConfig(secret: ""), CriarEnv());
        act.Should().Throw<InvalidOperationException>().WithMessage("*'Auth:JwtSecret'*");
    }

    [Fact]
    public void AddJwtAuthentication_SecretAusente_LancaInvalidOperationException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddJwtAuthentication(CriarConfig(), CriarEnv());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddJwtAuthentication_SecretCurto_LancaInvalidOperationException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddJwtAuthentication(CriarConfig(secret: "curto"), CriarEnv());
        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void AddJwtAuthentication_SecretValido_NaoLancaExcecao()
    {
        var services = new ServiceCollection();
        var act = () => services.AddJwtAuthentication(
            CriarConfig(
                secret: "test-only-secret-at-least-32-chars!!",
                issuer: "meu-issuer",
                audience: "meu-audience"
            ),
            CriarEnv());
        act.Should().NotThrow();
    }
}
