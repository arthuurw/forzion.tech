using FluentAssertions;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class EfCoreLoggingTests
{
    [Fact]
    public void AppDbContext_UsaOLoggerFactoryDoPipelineDaAplicacao()
    {
        var dict = new Dictionary<string, string?>
        {
            ["ConnectionStrings:AppConnection"] = "Host=localhost;Database=postgres;Username=u;Password=p",
        };
        var (services, configuration) = InfraHarness.Montar(dict);
        services.AddInfrastructure(configuration, InfraHarness.Env("Development"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var extensao = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();

        extensao.Should().NotBeNull();
        extensao!.LoggerFactory.Should().BeSameAs(provider.GetRequiredService<ILoggerFactory>());
    }
}
