using forzion.tech.Api.Extensions;
using forzion.tech.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// User Secrets carregados explicitamente em Homolog e Development (local).
if (builder.Environment.IsEnvironment("Homolog") || builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>(optional: true);

// Registro de Serviços
builder.Services
    .AddApiServices(builder.Configuration, builder.Environment)
    .AddApplicationHandlers();

var app = builder.Build();

// Seed (Homolog e Development apenas)
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Homolog"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync().ConfigureAwait(false);
}

// Configuração do Pipeline e Rotas
app.UseApiConfiguration();
app.MapApiEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
    protected Program() { }
}
