using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

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

        group.MapPost("/register/treinador", async (
            RegistrarTreinadorRequest request,
            RegistrarTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RegistrarTreinadorCommand(request.Email, request.Senha, request.Nome), cancellationToken);

            return Results.Created($"/treinador/perfil", result);
        })
        .AllowAnonymous()
        .WithSummary("Cadastra um novo treinador (aguarda aprovação)")
        .Produces<TreinadorResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/register/aluno", async (
            RegistrarAlunoRequest request,
            RegistrarAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RegistrarAlunoCommand(request.Email, request.Senha, request.Nome, request.TreinadorId, request.Telefone),
                cancellationToken);

            return Results.Created($"/aluno/perfil", result);
        })
        .AllowAnonymous()
        .WithSummary("Cadastra um novo aluno e cria vínculo pendente com o treinador")
        .Produces<AlunoResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}

public record LoginRequest(string Email, string Senha);
public record RegistrarTreinadorRequest(string Email, string Senha, string Nome);
public record RegistrarAlunoRequest(string Email, string Senha, string Nome, Guid TreinadorId, string? Telefone = null);
