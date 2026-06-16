using FluentAssertions;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class DeliveryLogSettingsTests
{
    private static ServiceProvider BuildProvider(string? environment, Dictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build();
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
    public void Production_SemChave_FalhaFechado()
    {
        using var sp = BuildProvider("Production");

        var act = () => _ = sp.GetRequiredService<IOptions<DeliveryLogSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Production_ComChave_Sobe()
    {
        using var sp = BuildProvider("Production",
            new Dictionary<string, string?> { ["DeliveryLog:RecipientHashKey"] = "prod-key" });

        sp.GetRequiredService<IOptions<DeliveryLogSettings>>().Value
            .RecipientHashKey.Should().Be("prod-key");
    }

    [Fact]
    public void NaoProducao_SemChave_UsaDefaultDev()
    {
        using var sp = BuildProvider("Development");

        sp.GetRequiredService<IOptions<DeliveryLogSettings>>().Value
            .RecipientHashKey.Should().Be(DeliveryLogSettings.DevDefaultKey);
    }
}
