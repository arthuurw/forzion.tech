using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class ResendFailClosedTests
{
    private static Dictionary<string, string?> Config(string? resendApiKey)
    {
        var dict = new Dictionary<string, string?>();
        if (resendApiKey is not null)
            dict["Resend:ApiKey"] = resendApiKey;
        return dict;
    }

    private static Action Aplicar(string environment, string? resendApiKey)
    {
        var (services, configuration) = InfraHarness.Montar(Config(resendApiKey));
        return () => services.AddInfrastructure(configuration, InfraHarness.Env(environment));
    }

    private static bool EmailHabilitado(string environment, string? resendApiKey)
    {
        var (services, configuration) = InfraHarness.Montar(Config(resendApiKey));
        services.AddInfrastructure(configuration, InfraHarness.Env(environment));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEmailService>().Habilitado;
    }

    [Fact]
    public void Producao_SemResend_AbortaBoot()
    {
        Aplicar("Production", resendApiKey: null)
            .Should().Throw<InvalidOperationException>().WithMessage("*produção*");
    }

    [Fact]
    public void Producao_ResendWhitespace_AbortaBoot()
    {
        Aplicar("Production", resendApiKey: "   ")
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Producao_ComResend_RegistraResendEmailService()
    {
        EmailHabilitado("Production", resendApiKey: "re_live_key").Should().BeTrue();
    }

    [Fact]
    public void NaoProducao_SemResend_RegistraNullEmailService()
    {
        EmailHabilitado("Development", resendApiKey: null).Should().BeFalse();
    }
}
