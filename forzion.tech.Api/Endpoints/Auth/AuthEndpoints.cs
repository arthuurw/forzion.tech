using forzion.tech.Application.UseCases.Auth.Login;

namespace forzion.tech.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest request,
            LoginHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new LoginCommand(request.Email, request.Senha), cancellationToken);

            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("Login")
        .Produces<LoginResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}

public record LoginRequest(string Email, string Senha);
