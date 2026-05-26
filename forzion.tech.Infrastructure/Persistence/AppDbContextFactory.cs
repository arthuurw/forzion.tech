using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.Infrastructure.Persistence;

/// <summary>
/// Usada apenas em design-time (migrations). Não é invocada em runtime.
/// Lê a configuração real (appsettings + User Secrets + variáveis de ambiente)
/// respeitando o ambiente definido em ASPNETCORE_ENVIRONMENT.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets("049d65fb-2c12-483c-b56e-cb753632d11f")
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("AppConnection")
            ?? throw new InvalidOperationException("Connection string 'AppConnection' não encontrada.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
