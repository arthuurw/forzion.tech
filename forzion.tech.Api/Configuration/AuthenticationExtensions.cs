using System.Text;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace forzion.tech.Api.Configuration;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var secret = configuration["Auth:JwtSecret"] ?? string.Empty;
        var issuer = configuration["Auth:JwtIssuer"] ?? "forzion.tech";
        var audience = configuration["Auth:JwtAudience"] ?? "forzion.tech";

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Configuração 'Auth:JwtSecret' não encontrada. Configure via User Secrets ou variável de ambiente.");

        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("'Auth:JwtSecret' deve ter no mínimo 32 bytes. Gere via: openssl rand -base64 64");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = BuildEvents(environment);
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("SystemAdmin", p => p.RequireClaim("tipo_conta", "SystemAdmin"))
            .AddPolicy("Treinador", p => p.RequireClaim("tipo_conta", "Treinador"))
            .AddPolicy("Aluno", p => p.RequireClaim("tipo_conta", "Aluno"));

        return services;
    }

    private static JwtBearerEvents BuildEvents(IWebHostEnvironment environment)
    {
        var events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var jtiClaim = ctx.Principal?.FindFirst("jti")?.Value;
                if (string.IsNullOrEmpty(jtiClaim) || !Guid.TryParse(jtiClaim, out var jti))
                {
                    ctx.Fail("Token sem jti válido.");
                    return;
                }

                var services = ctx.HttpContext.RequestServices;

                var repo = services.GetService<ITokenRevogadoRepository>();
                if (repo is not null &&
                    await repo.EstaRevogadoAsync(jti, ctx.HttpContext.RequestAborted).ConfigureAwait(false))
                {
                    ctx.Fail("Token revogado.");
                    return;
                }

                var subClaim = ctx.Principal?.FindFirst("sub")?.Value;
                var nbfClaim = ctx.Principal?.FindFirst("nbf")?.Value;
                if (!Guid.TryParse(subClaim, out var contaId) || !long.TryParse(nbfClaim, out var nbf))
                {
                    ctx.Fail("Token sem sub/nbf válido.");
                    return;
                }

                var contaRepo = services.GetService<IContaRepository>();
                var epoch = contaRepo is null
                    ? null
                    : await contaRepo.ObterEpochSessaoAsync(contaId, ctx.HttpContext.RequestAborted).ConfigureAwait(false);

                // Compara em segundos (nbf é NumericDate); truncar o epoch ao mesmo grão não
                // rejeita um token re-emitido no mesmo segundo do carimbo.
                if (epoch is not null && nbf < epoch.Value.ToUnixTimeSeconds())
                    ctx.Fail("Sessão invalidada.");
            }
        };

        if (!environment.IsProduction())
            AddDiagnosticEvents(events);

        return events;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static void AddDiagnosticEvents(JwtBearerEvents events)
    {
        events.OnMessageReceived = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
            logger.LogDebug("[JWT] Token recebido.");
            return Task.CompletedTask;
        };
        events.OnAuthenticationFailed = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
            logger.LogWarning("[JWT] Falha na autenticação: {Type}", ctx.Exception.GetType().Name);
            return Task.CompletedTask;
        };
        events.OnChallenge = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
            logger.LogDebug("[JWT] Challenge: Error={Error}", ctx.Error);
            return Task.CompletedTask;
        };
    }
}
