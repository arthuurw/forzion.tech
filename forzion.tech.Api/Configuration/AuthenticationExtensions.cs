using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace forzion.tech.Api.Configuration;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var authority = configuration["Auth:Authority"]
            ?? throw new InvalidOperationException("Configuração 'Auth:Authority' não encontrada.");

        var audience = configuration["Auth:Audience"]
            ?? throw new InvalidOperationException("Configuração 'Auth:Audience' não encontrada.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;

                if (!environment.IsProduction())
                    options.Events = BuildDiagnosticEvents();
            });

        services.AddAuthorization();

        return services;
    }

    // CA1848: Logs de diagnóstico são ativados apenas em ambientes não-produtivos e não são hot paths.
    // LoggerMessage delegates adicionariam boilerplate sem ganho real neste contexto.
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
