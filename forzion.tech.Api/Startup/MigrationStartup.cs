using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Startup;

// R1 (deploy-safety): migrate desacoplado do boot. Aplicar DDL no startup do web host fazia uma
// migration quebrada derrubar o container DEPOIS de `up -d` (app fora do ar). Aqui o migrate é um
// modo CLI one-shot (`app migrate`) que aplica e SAI; roda como step pré-deploy e, falhando, aborta
// o deploy antes de tocar nos containers em produção.
public static class MigrationStartup
{
    // `dotnet forzion.tech.Api.dll migrate` (ou `--migrate`): invocação one-shot do step de deploy.
    public static bool IsMigrateCommand(string[] args) =>
        args.Contains("migrate") || args.Contains("--migrate");

    // Boot normal não toca DDL. Auto-migrate fica restrito a Development, por conveniência local.
    // Em ambiente Test o banco usa provider in-memory via WebApplicationFactory, e pedir migração
    // ali lançaria por ser operação relational-only. Homolog e Production migram pelo step de
    // deploy chamado `app migrate`.
    public static bool ShouldAutoMigrateOnBoot(IHostEnvironment environment) =>
        environment.IsDevelopment();

    // Exit 0 = schema+seed aplicados; Exit 1 = falha → `set -e` aborta o deploy.
    public static async Task<int> RunMigrateAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            await seeder.SeedAsync().ConfigureAwait(false);
            app.Logger.LogInformation("Migrate one-shot: schema e seed aplicados com sucesso.");
            return 0;
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Migrate one-shot falhou — deploy deve abortar.");
            return 1;
        }
    }
}
