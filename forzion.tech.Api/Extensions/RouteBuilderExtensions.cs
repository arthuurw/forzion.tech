using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Admin;
using forzion.tech.Api.Endpoints.AlunoArea;
using forzion.tech.Api.Endpoints.Auth;
using forzion.tech.Api.Endpoints.Alunos;
using forzion.tech.Api.Endpoints.Conta;
using forzion.tech.Api.Endpoints.Exercicios;
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

        return endpoints;
    }

    public static IApplicationBuilder UseApiConfiguration(this WebApplication app)
    {
        app.UseSwaggerInNonProduction();
        app.UseExceptionHandler();

        if (app.Environment.IsProduction())
            app.UseHttpsRedirection();

        app.UseCors("AllowFrontend");
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
