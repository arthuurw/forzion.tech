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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

namespace forzion.tech.Api.Extensions;

public static class RouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
        endpoints.MapAdminEndpoints();
        endpoints.MapHealthReportEndpoints();
        endpoints.MapTreinadorEndpoints();
        endpoints.MapAlunoAreaEndpoints();
        endpoints.MapAlunoEndpoints();
        endpoints.MapContaEndpoints();
        endpoints.MapExercicioEndpoints();
        endpoints.MapTreinoEndpoints();
        endpoints.MapPagamentosEndpoints();
        endpoints.MapInternalEndpoints();
        endpoints.MapWebhookEndpoints();

        return endpoints;
    }

    public static IApplicationBuilder UseApiConfiguration(this WebApplication app)
    {
        // CORS startup check: emit a LogWarning when no valid origins are configured
        // in non-Test environments so the deny-all state is visible in logs.
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

        app.UseSwaggerInDevelopment();
        app.UseExceptionHandler();

        // Atrás do nginx (Homolog/Production): reescreve RemoteIpAddress/scheme a partir
        // do X-Forwarded-*. Precisa rodar ANTES de HttpsRedirection/Auth/RateLimiter. Sem isso
        // o RemoteIpAddress seria o IP do container nginx → rate-limit colapsa todos os
        // clientes num bucket (CC2). O backend só é alcançável via nginx (rede docker isolada,
        // sem porta publicada), então o único hop é confiável: limpamos as listas default
        // (que só confiam em loopback) p/ aceitar o cabeçalho do proxy. ForwardLimit=1 = 1 hop.
        if (app.Environment.IsProduction() || app.Environment.IsEnvironment("Homolog"))
        {
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                ForwardLimit = 1,
            };
            forwarded.KnownNetworks.Clear();
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
        // Ordem: Authentication antes do RateLimiter para que policies particionadas
        // por sub claim consigam identificar o usuário; sem isso a chave caía sempre
        // no fallback de IP, inutilizando a partição por usuário.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapHealthCheck();

        return app;
    }

    private static IEndpointRouteBuilder MapHealthCheck(this IEndpointRouteBuilder endpoints)
    {
        // LIVENESS: nenhum check (Predicate => false) — 200 enquanto o processo estiver
        // vivo. Mantido assim porque docker-compose/frontend dependem deste contrato.
        endpoints.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .RequireRateLimiting("read");

        // READINESS: executa apenas os checks taggeados "ready" (DbContextCheck "db").
        // 200 quando o DB responde; 503 (Unhealthy) caso contrário.
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") })
            .AllowAnonymous()
            .RequireRateLimiting("read");

        return endpoints;
    }
}
