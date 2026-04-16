using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

        // Em ambientes não-produtivos, usa uma chave de fallback para que os testes funcionem
        // sem precisar configurar segredos reais.
        if (string.IsNullOrWhiteSpace(secret) && !environment.IsProduction())
            secret = "forzion-test-secret-key-32-bytes!!";

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Configuração 'Auth:JwtSecret' não encontrada.");

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

                if (!environment.IsProduction())
                    options.Events = BuildDiagnosticEvents();
            });

        services.AddAuthorization();

        return services;
    }

    // CA1848: Logs de diagnóstico são ativados apenas em ambientes não-produtivos e não são hot paths.
#pragma warning disable CA1848
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static JwtBearerEvents BuildDiagnosticEvents() => new()
    {
        OnMessageReceived = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
            logger.LogDebug("[JWT] Token recebido: {Token}", ctx.Token != null ? ctx.Token[..20] + "..." : "NENHUM");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
            logger.LogWarning("[JWT] Falha na autenticação: {Type} — {Message}", ctx.Exception.GetType().Name, ctx.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
            logger.LogDebug("[JWT] Challenge: Error={Error} | Failure={Failure}", ctx.Error, ctx.AuthenticateFailure?.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
            logger.LogDebug("[JWT] Token válido — sub: {Sub}", ctx.Principal?.FindFirst("sub")?.Value);
            return Task.CompletedTask;
        }
    };
#pragma warning restore CA1848
}
