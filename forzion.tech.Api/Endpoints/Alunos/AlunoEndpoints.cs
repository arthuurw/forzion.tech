using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Application.UseCases.Alunos.AtualizarAluno;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Alunos;

public static class AlunoEndpoints
{
    public static void MapAlunoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/alunos").WithTags("Alunos");

        group.MapPost("/", async (
            [FromBody] CadastrarAlunoRequest request,
            CadastrarAlunoHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var command = new CadastrarAlunoCommand(
                userContext.PerfilId, request.Nome, request.Email, request.Telefone);
            
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/alunos/{response.AlunoId}", response);
        })
        .RequireAuthorization()
        .WithSummary("Cadastra um novo aluno")
        .Produces<AlunoResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/", async (
            ListarAlunosHandler handler,
            IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;

            var query = new ListarAlunosQuery(userContext.PerfilId, p, tp);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Lista alunos do tenant com paginação")
        .Produces<ListarAlunosResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id}", async (
            Guid id,
            ObterAlunoHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var query = new ObterAlunoQuery(id);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Retorna os dados de um aluno")
        .Produces<AlunoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{id}", async (
            Guid id,
            [FromBody] AtualizarAlunoRequest request,
            AtualizarAlunoHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var command = new AtualizarAlunoCommand(id, request.Nome, request.Email, request.Telefone);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Atualiza os dados de um aluno")
        .Produces<AlunoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{id}/status", async (
            Guid id,
            [FromBody] AlterarStatusAlunoRequest request,
            AlterarStatusAlunoHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var command = new AlterarStatusAlunoCommand(id, request.Status);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Altera o status de um aluno (somente Admin)")
        .Produces<AlunoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

}

public record CadastrarAlunoRequest(string Nome, string? Email, string? Telefone);
public record AtualizarAlunoRequest(string? Nome, string? Email, string? Telefone);
public record AlterarStatusAlunoRequest(AlunoStatus Status);
