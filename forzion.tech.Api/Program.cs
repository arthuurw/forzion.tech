using System.Text.Json.Serialization;
using forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;
using forzion.tech.Application.Interfaces;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Context;
using forzion.tech.Api.Endpoints.Usuarios;
using forzion.tech.Api.Middleware;
using forzion.tech.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddSwagger();
builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<RegistrarUsuarioHandler>();

var app = builder.Build();

app.UseSwaggerInDevelopment();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapUsuarioEndpoints();

await app.RunAsync().ConfigureAwait(false);
