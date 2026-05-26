using FluentAssertions;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Application.Settings;

public class EmailSettingsTests
{
    private static EmailSettings Bind(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<EmailSettings>().BindConfiguration("Email");
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<EmailSettings>>().Value;
    }

    [Fact]
    public void Bind_SemSecaoEmail_UsaDefaultsProdSafe()
    {
        var settings = Bind([]);

        settings.FromName.Should().Be("forzion.tech");
        settings.FromAddress.Should().Be("noreply@forzion.tech");
        settings.MarcarComoTeste.Should().BeFalse();
        settings.PrefixoAssuntoTeste.Should().BeEmpty();
        settings.RedirecionarDestinatariosPara.Should().BeEmpty();
        settings.AllowlistDominios.Should().BeEmpty();
    }

    [Fact]
    public void Bind_SecaoEmailNaoProd_PreencheTodasPropriedades()
    {
        var settings = Bind(new Dictionary<string, string?>
        {
            ["Email:FromName"] = "forzion.tech [HOMOLOG]",
            ["Email:FromAddress"] = "homolog@forzion.tech",
            ["Email:MarcarComoTeste"] = "true",
            ["Email:PrefixoAssuntoTeste"] = "[HOMOLOG - TESTE]",
            ["Email:RedirecionarDestinatariosPara"] = "qa@forzion.tech",
            ["Email:AllowlistDominios"] = "forzion.tech",
        });

        settings.FromName.Should().Be("forzion.tech [HOMOLOG]");
        settings.FromAddress.Should().Be("homolog@forzion.tech");
        settings.MarcarComoTeste.Should().BeTrue();
        settings.PrefixoAssuntoTeste.Should().Be("[HOMOLOG - TESTE]");
        settings.RedirecionarDestinatariosPara.Should().Be("qa@forzion.tech");
        settings.AllowlistDominios.Should().Be("forzion.tech");
    }
}
