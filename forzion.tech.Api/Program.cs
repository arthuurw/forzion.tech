using forzion.tech.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// User Secrets carregados explicitamente em Homolog (local).
if (builder.Environment.IsEnvironment("Homolog"))
    builder.Configuration.AddUserSecrets<Program>(optional: true);

// Registro de Serviços
builder.Services
    .AddApiServices(builder.Configuration, builder.Environment)
    .AddApplicationHandlers();

var app = builder.Build();

// Configuração do Pipeline e Rotas
app.UseApiConfiguration();
app.MapApiEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
    protected Program() { }
}
