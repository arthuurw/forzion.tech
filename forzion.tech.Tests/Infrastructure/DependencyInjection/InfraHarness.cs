using forzion.tech.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

internal static class InfraHarness
{
    public static IHostEnvironment Env(string? environment) =>
        Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environment);

    public static (IServiceCollection Services, IConfiguration Configuration) Montar(
        IDictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>())
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        return (services, configuration);
    }

    public static ServiceProvider BuildProvider(
        string? environment, IDictionary<string, string?>? config = null)
    {
        var settings = new Dictionary<string, string?>(config ?? new Dictionary<string, string?>());
        settings.TryAdd("Resend:ApiKey", "re_test_key");
        var (services, configuration) = Montar(settings);
        services.AddInfrastructure(configuration, Env(environment));
        return services.BuildServiceProvider();
    }
}
