using FluentAssertions;
using forzion.tech.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class PoolerPortGuardTests
{
    private static Action AplicarInfra(string? connectionString)
    {
        var dict = new Dictionary<string, string?>();
        if (connectionString is not null)
            dict["ConnectionStrings:AppConnection"] = connectionString;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        return () => services.AddInfrastructure(
            configuration, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Development"));
    }

    [Fact]
    public void Porta6543_RecusaBoot_ApontandoSessionPooler()
    {
        AplicarInfra("Host=aws.pooler.supabase.com;Port=6543;Database=postgres;Username=u;Password=p")
            .Should().Throw<InvalidOperationException>().WithMessage("*5432*");
    }

    [Fact]
    public void Porta5432_NaoLanca()
    {
        AplicarInfra("Host=aws.pooler.supabase.com;Port=5432;Database=postgres;Username=u;Password=p")
            .Should().NotThrow();
    }

    [Fact]
    public void PortaOmitida_NaoLanca()
    {
        AplicarInfra("Host=localhost;Database=postgres;Username=u;Password=p")
            .Should().NotThrow();
    }

    [Fact]
    public void ConnectionStringNula_NaoParseia_NaoLanca()
    {
        AplicarInfra(null).Should().NotThrow();
    }
}
