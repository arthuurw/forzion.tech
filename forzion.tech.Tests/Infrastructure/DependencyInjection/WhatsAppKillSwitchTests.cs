using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class WhatsAppKillSwitchTests
{
    private static bool WhatsAppHabilitado(IDictionary<string, string?> config)
    {
        var (services, configuration) = InfraHarness.Montar(config);
        services.AddInfrastructure(configuration, InfraHarness.Env("Development"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IWhatsAppNotifier>().Habilitado;
    }

    [Fact]
    public void Desabilitado_ComCredenciaisValidas_RegistraNull()
    {
        WhatsAppHabilitado(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "false",
            ["WhatsApp:PhoneNumberId"] = "123456",
            ["WhatsApp:AccessToken"] = "token-valido"
        }).Should().BeFalse();
    }

    [Fact]
    public void Habilitado_ComCredenciaisValidas_RegistraMeta()
    {
        WhatsAppHabilitado(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "true",
            ["WhatsApp:PhoneNumberId"] = "123456",
            ["WhatsApp:AccessToken"] = "token-valido"
        }).Should().BeTrue();
    }

    [Fact]
    public void DesabilitadoOuAusente_SemCredenciais_RegistraNull()
    {
        WhatsAppHabilitado(new Dictionary<string, string?>()).Should().BeFalse();
    }

    [Fact]
    public void Habilitado_SemCredenciais_RegistraNull()
    {
        WhatsAppHabilitado(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "true"
        }).Should().BeFalse();
    }
}
