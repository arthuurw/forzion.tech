using forzion.tech.Api.Extensions;
using forzion.tech.Api.Startup;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Homolog") || builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services
    .AddApiServices(builder.Configuration, builder.Environment)
    .AddApplicationHandlers();

var app = builder.Build();

// R1 (deploy-safety): `app migrate` aplica schema+seed e SAI, fora do web host (step one-shot
// pré-deploy; falha aborta o deploy). Ver MigrationStartup.
if (MigrationStartup.IsMigrateCommand(args))
    return await MigrationStartup.RunMigrateAsync(app).ConfigureAwait(false);

if (MigrationStartup.ShouldAutoMigrateOnBoot(app.Environment))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync().ConfigureAwait(false);
}

app.UseApiConfiguration();
app.MapApiEndpoints();

await app.RunAsync().ConfigureAwait(false);
return 0;

public partial class Program
{
    protected Program() { }
}
