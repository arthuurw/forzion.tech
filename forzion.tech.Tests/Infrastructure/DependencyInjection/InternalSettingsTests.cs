using FluentAssertions;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class InternalSettingsTests
{
    private static ServiceProvider BuildProvider(string? environment, Dictionary<string, string?>? config = null)
    {
        var settings = new Dictionary<string, string?>(config ?? []);
        settings.TryAdd("Resend:ApiKey", "re_test_key");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environment));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Production_SemChave_FalhaFechado()
    {
        using var sp = BuildProvider("Production");

        var act = () => _ = sp.GetRequiredService<IOptions<InternalSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Production_ComChave_Sobe()
    {
        using var sp = BuildProvider("Production",
            new Dictionary<string, string?> { ["Internal:ApiKey"] = "prod-internal-key" });

        sp.GetRequiredService<IOptions<InternalSettings>>().Value
            .ApiKey.Should().Be("prod-internal-key");
    }

    [Fact]
    public void NaoProducao_SemChave_Sobe()
    {
        using var sp = BuildProvider("Development");

        sp.GetRequiredService<IOptions<InternalSettings>>().Value
            .ApiKey.Should().BeEmpty();
    }
}
