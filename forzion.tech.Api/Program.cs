using System.Text.Json.Serialization;
using forzion.tech.Application.UseCases.Usuarios.AtualizarUsuario;
using forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;
using forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;
using forzion.tech.Application.Interfaces;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Context;
using forzion.tech.Api.Endpoints.Usuarios;
using forzion.tech.Api.Middleware;
using forzion.tech.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// User Secrets são carregados automaticamente em Development.
// Em Homolog (ambiente local dos desenvolvedores), carregamos explicitamente.
// Em produção, o arquivo não existe — optional: true garante que não há erro.
if (builder.Environment.IsEnvironment("Homolog"))
    builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddSwagger();
builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

if (!builder.Environment.IsEnvironment("Test"))
    builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<RegistrarUsuarioHandler>();
builder.Services.AddScoped<ObterUsuarioAtualHandler>();
builder.Services.AddScoped<AtualizarUsuarioHandler>();

var app = builder.Build();

app.UseSwaggerInNonProduction();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapUsuarioEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program { }
