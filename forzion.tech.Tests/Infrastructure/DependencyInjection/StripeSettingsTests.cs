using FluentAssertions;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class StripeSettingsTests
{
    private static readonly Dictionary<string, string?> ConfigBase = new()
    {
        ["Stripe:SecretKey"] = "sk_test_basekey",
        ["Stripe:WebhookSecret"] = "whsec_base",
    };

    private static ServiceProvider BuildProvider(string? environment, Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        var original = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
            services.AddInfrastructure(configuration);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", original);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ChaveSkLive_ExpectLivemodeNaoConfigurado_EmDesenvolvimento_FalhaBoot()
    {
        var config = new Dictionary<string, string?>(ConfigBase)
        {
            ["Stripe:SecretKey"] = "sk_live_verysecretkey123",
        };
        using var sp = BuildProvider("Development", config);

        var act = () => _ = sp.GetRequiredService<IOptions<StripeSettings>>().Value;

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().NotBeEmpty();
        ex.Failures.Should().AllSatisfy(f => f.Should().NotContain("verysecretkey123"));
    }

    [Fact]
    public void ChaveSkTest_ExpectLivemodeTrueExplicito_FalhaBoot()
    {
        var config = new Dictionary<string, string?>(ConfigBase)
        {
            ["Stripe:SecretKey"] = "sk_test_verysecretkey456",
            ["Stripe:ExpectLivemode"] = "true",
        };
        using var sp = BuildProvider("Development", config);

        var act = () => _ = sp.GetRequiredService<IOptions<StripeSettings>>().Value;

        var ex = act.Should().Throw<OptionsValidationException>().Which;
        ex.Failures.Should().NotBeEmpty();
        ex.Failures.Should().AllSatisfy(f => f.Should().NotContain("verysecretkey456"));
    }

    [Fact]
    public void ChaveSkLive_ExpectLivemodeTrueExplicito_Sobe()
    {
        var config = new Dictionary<string, string?>(ConfigBase)
        {
            ["Stripe:SecretKey"] = "sk_live_validprodkey",
            ["Stripe:ExpectLivemode"] = "true",
        };
        using var sp = BuildProvider("Production", config);

        var settings = sp.GetRequiredService<IOptions<StripeSettings>>().Value;

        settings.SecretKey.Should().StartWith("sk_live_");
        settings.ExpectLivemode.Should().BeTrue();
    }

    [Fact]
    public void ChaveSkTest_ExpectLivemodeNaoConfigurado_EmDesenvolvimento_Sobe()
    {
        using var sp = BuildProvider("Development", new Dictionary<string, string?>(ConfigBase));

        var settings = sp.GetRequiredService<IOptions<StripeSettings>>().Value;

        settings.SecretKey.Should().StartWith("sk_test_");
        settings.ExpectLivemode.Should().BeFalse();
    }

    [Fact]
    public void TaxaAcimaDe100_FalhaBoot()
    {
        var config = new Dictionary<string, string?>(ConfigBase)
        {
            ["Stripe:TaxaPlataformaPercent"] = "101",
        };
        using var sp = BuildProvider("Development", config);

        var act = () => _ = sp.GetRequiredService<IOptions<StripeSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void TaxaIgualA100_Sobe()
    {
        var config = new Dictionary<string, string?>(ConfigBase)
        {
            ["Stripe:TaxaPlataformaPercent"] = "100",
        };
        using var sp = BuildProvider("Development", config);

        var settings = sp.GetRequiredService<IOptions<StripeSettings>>().Value;

        settings.TaxaPlataformaPercent.Should().Be(100m);
    }
}
