using System.Security.Claims;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace forzion.tech.Tests.Api.Configuration;

public class MfaAuthorizationPolicyTests
{
    private static ClaimsPrincipal Principal(params (string Tipo, string Valor)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Tipo, c.Valor)), "Test"));

    private static readonly ClaimsPrincipal Pleno = Principal(("sub", Guid.NewGuid().ToString()), ("tipo_conta", "Aluno"));
    private static readonly ClaimsPrincipal Pendente = Principal(("sub", Guid.NewGuid().ToString()), (MfaScopes.ClaimType, MfaScopes.Pendente));
    private static readonly ClaimsPrincipal StepUp = Principal(("sub", Guid.NewGuid().ToString()), (MfaScopes.ClaimType, MfaScopes.StepUp));

    private static (IAuthorizationService Authz, IAuthorizationPolicyProvider Provider) Construir()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:JwtSecret"] = "forzion-test-secret-key-32-bytes!!" })
            .Build();
        var env = Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == "Development");
        services.AddJwtAuthentication(config, env);

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IAuthorizationService>(), sp.GetRequiredService<IAuthorizationPolicyProvider>());
    }

    [Fact]
    public async Task PolicyPadrao_AceitaTokenPleno_RejeitaEscopado()
    {
        var (authz, provider) = Construir();
        var padrao = await provider.GetDefaultPolicyAsync();

        (await authz.AuthorizeAsync(Pleno, null, padrao)).Succeeded.Should().BeTrue();
        (await authz.AuthorizeAsync(Pendente, null, padrao)).Succeeded.Should().BeFalse();
        (await authz.AuthorizeAsync(StepUp, null, padrao)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task PolicyPendente_AceitaSoTokenPendente()
    {
        var (authz, provider) = Construir();
        var pendente = await provider.GetPolicyAsync(MfaScopes.PolicyPendente);

        (await authz.AuthorizeAsync(Pendente, null, pendente!)).Succeeded.Should().BeTrue();
        (await authz.AuthorizeAsync(StepUp, null, pendente!)).Succeeded.Should().BeFalse();
        (await authz.AuthorizeAsync(Pleno, null, pendente!)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task PolicyStepUp_AceitaSoTokenStepUp()
    {
        var (authz, provider) = Construir();
        var stepup = await provider.GetPolicyAsync(MfaScopes.PolicyStepUp);

        (await authz.AuthorizeAsync(StepUp, null, stepup!)).Succeeded.Should().BeTrue();
        (await authz.AuthorizeAsync(Pendente, null, stepup!)).Succeeded.Should().BeFalse();
        (await authz.AuthorizeAsync(Pleno, null, stepup!)).Succeeded.Should().BeFalse();
    }
}
