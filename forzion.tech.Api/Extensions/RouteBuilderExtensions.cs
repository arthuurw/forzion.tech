using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Admin;
using forzion.tech.Api.Endpoints.AlunoArea;
using forzion.tech.Api.Endpoints.Auth;
using forzion.tech.Api.Endpoints.Alunos;
using forzion.tech.Api.Endpoints.Conta;
using forzion.tech.Api.Endpoints.Exercicios;
using forzion.tech.Api.Endpoints.Pagamentos;
using forzion.tech.Api.Endpoints.Treinos;
using forzion.tech.Api.Endpoints.Treinador;

namespace forzion.tech.Api.Extensions;

public static class RouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
        endpoints.MapAdminEndpoints();
        endpoints.MapTreinadorEndpoints();
        endpoints.MapAlunoAreaEndpoints();
        endpoints.MapAlunoEndpoints();
        endpoints.MapContaEndpoints();
        endpoints.MapExercicioEndpoints();
        endpoints.MapTreinoEndpoints();
        endpoints.MapPagamentosEndpoints();
        endpoints.MapWebhookEndpoints();

        return endpoints;
    }

    public static IApplicationBuilder UseApiConfiguration(this WebApplication app)
    {
        app.UseSwaggerInDevelopment();
        app.UseExceptionHandler();

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
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthCheck();

        return app;
    }

    private static IEndpointRouteBuilder MapHealthCheck(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health");
        return endpoints;
    }
}
