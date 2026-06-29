using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class ResendFailClosedTests
{
    private static (IServiceCollection services, IConfiguration configuration) Montar(string? resendApiKey)
    {
        var dict = new Dictionary<string, string?>();
        if (resendApiKey is not null)
            dict["Resend:ApiKey"] = resendApiKey;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        return (services, configuration);
    }

    private static Action Aplicar(string environment, string? resendApiKey)
    {
        var (services, configuration) = Montar(resendApiKey);
        return () => services.AddInfrastructure(
            configuration, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environment));
    }

    private static bool EmailHabilitado(string environment, string? resendApiKey)
    {
        var (services, configuration) = Montar(resendApiKey);
        services.AddInfrastructure(
            configuration, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environment));
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
