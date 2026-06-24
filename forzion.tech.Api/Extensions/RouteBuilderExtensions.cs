using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Admin;
using Microsoft.Extensions.Logging;
using forzion.tech.Api.Endpoints.AlunoArea;
using forzion.tech.Api.Endpoints.Auth;
using forzion.tech.Api.Endpoints.Alunos;
using forzion.tech.Api.Endpoints.Conta;
using forzion.tech.Api.Endpoints.Exercicios;
using forzion.tech.Api.Endpoints.Internal;
using forzion.tech.Api.Endpoints.Pagamentos;
using forzion.tech.Api.Endpoints.Treinos;
using forzion.tech.Api.Endpoints.Treinador;
using forzion.tech.Api.Endpoints.Suporte;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace forzion.tech.Api.Extensions;

public static class RouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
        endpoints.MapMfaLoginEndpoints();
        endpoints.MapStepUpEndpoints();
        endpoints.MapAdminEndpoints();

        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsProduction())
            endpoints.MapTestDataEndpoints();

        endpoints.MapHealthReportEndpoints();
        endpoints.MapTreinadorEndpoints();
        endpoints.MapAlunoAreaEndpoints();
        endpoints.MapAlunoEndpoints();
        endpoints.MapContaEndpoints();
        endpoints.MapMfaEndpoints();
        endpoints.MapSuporteEndpoints();
        endpoints.MapExercicioEndpoints();
        endpoints.MapTreinoEndpoints();
        endpoints.MapPagamentosEndpoints();
        endpoints.MapInternalEndpoints();
        endpoints.MapWebhookEndpoints();

        return endpoints;
    }

    public static IApplicationBuilder UseApiConfiguration(this WebApplication app)
    {
        if (!app.Environment.IsEnvironment("Test"))
        {
            var raw = app.Configuration["Cors:AllowedOrigins"]?.Split(';') ?? Array.Empty<string>();
            var hasValidOrigins = raw
                .Select(o => o.Trim())
                .Any(o => !string.IsNullOrWhiteSpace(o)
                          && !o.Contains('*')
                          && Uri.TryCreate(o, UriKind.Absolute, out _));

            if (!hasValidOrigins)
            {
                var corsLogger = app.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("CorsConfiguration");
                corsLogger.LogWarning(
                    "CORS: Cors:AllowedOrigins is empty or contains no valid origins. " +
                    "All cross-origin browser requests will be denied. " +
                    "Set 'Cors:AllowedOrigins' in configuration to allow frontend access.");
            }
        }

        app.UseOpenApiInDevelopment();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsProduction() || app.Environment.IsEnvironment("Homolog"))
        {
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                ForwardLimit = 1,
            };
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
            app.UseForwardedHeaders(forwarded);
        }

        if (app.Environment.IsProduction())
            app.UseHttpsRedirection();

        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            ctx.Response.Headers.Append("X-Frame-Options", "DENY");
            ctx.Response.Headers.Append("Referrer-Policy", "no-referrer");
            ctx.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            if (app.Environment.IsProduction())
                ctx.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            await next();
        });

        app.UseCors("AllowFrontend");

        app.Use(async (ctx, next) =>
        {
            var requestId = RequestIdSeguro(ctx.Request.Headers["X-Request-Id"].FirstOrDefault())
                ?? ctx.TraceIdentifier;

            var correlationLogger = ctx.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Correlation");

            using (correlationLogger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
            }))
            {
                ctx.Response.Headers.Append("X-Request-Id", requestId);
                await next().ConfigureAwait(false);
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapHealthCheck();

        return app;
    }

    private static void MapHealthCheck(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .RequireRateLimiting("read");

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready"),
            ResponseWriter = EscreverStatusAgregado,
        })
            .AllowAnonymous()
            .RequireRateLimiting("read");
    }

    private static Task EscreverStatusAgregado(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync($"{{\"status\":\"{report.Status}\"}}");
    }

    private static string? RequestIdSeguro(string? entrada)
    {
        if (string.IsNullOrEmpty(entrada) || entrada.Length > 128)
            return null;

        var charsetSeguro = entrada.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
        return charsetSeguro ? entrada : null;
    }
}
