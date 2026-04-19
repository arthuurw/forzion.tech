using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadoresPublicos;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest request,
            [FromServices] LoginHandler handler,
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
            [FromServices] RegistrarTreinadorHandler handler,
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
            [FromServices] RegistrarAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RegistrarAlunoCommand(request.Email, request.Senha, request.Nome, request.TreinadorId, request.PacoteId, request.Telefone),
                cancellationToken);

            return Results.Created($"/aluno/perfil", result);
        })
        .AllowAnonymous()
        .WithSummary("Cadastra um novo aluno e cria vínculo pendente com o treinador")
        .Produces<AlunoResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- Endpoints Públicos (para Cadastro) ---

        group.MapGet("/planos", async (
            [FromServices] ListarPlanosTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithSummary("Lista todos os planos de treinador disponíveis")
        .Produces<IReadOnlyList<PlanoTreinadorResponse>>();

        group.MapGet("/treinadores/{id:guid}/pacotes", async (
            Guid id,
            [FromServices] ListarPacotesAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithSummary("Lista pacotes de um treinador específico (para escolha do aluno no cadastro)")
        .Produces<IReadOnlyList<PacoteAlunoResponse>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/treinadores", async (
            [FromServices] ListarTreinadoresPublicosHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithSummary("Lista treinadores ativos para o fluxo público de cadastro")
        .Produces<IReadOnlyList<TreinadorResponse>>();

        return endpoints;
    }
}

public record LoginRequest(string Email, string Senha);
public record RegistrarTreinadorRequest(string Email, string Senha, string Nome);
public record RegistrarAlunoRequest(string Email, string Senha, string Nome, Guid TreinadorId, Guid PacoteId, string? Telefone = null);
