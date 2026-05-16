using forzion.tech.Api.Extensions;
using forzion.tech.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Homolog") || builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services
    .AddApiServices(builder.Configuration, builder.Environment)
    .AddApplicationHandlers();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Homolog"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync().ConfigureAwait(false);
}

app.UseApiConfiguration();
app.MapApiEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
    protected Program() { }
}
