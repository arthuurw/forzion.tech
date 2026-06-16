using forzion.tech.Api.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Application.UseCases.Alunos.AtualizarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Alunos;

public static class AlunoEndpoints
{
    public static void MapAlunoEndpoints(this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup("/alunos")
            .WithTags("Alunos")
            .RequireRateLimiting("read")
            .AddEndpointFilter<PerfilIdRequiredFilter>()
            .AddEndpointFilter<PaginacaoFilter>();

        var writeGroup = app.MapGroup("/alunos")
            .WithTags("Alunos")
            .RequireRateLimiting("write")
            .AddEndpointFilter<PerfilIdRequiredFilter>()
            .AddEndpointFilter<PaginacaoFilter>();

        readGroup.MapGet("/", async (
            [FromServices] ListarAlunosHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : Math.Clamp(tamanhoPagina, 1, 100);

            var query = new ListarAlunosQuery(userContext.PerfilId, p, tp);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Lista alunos com paginação")
        .Produces<ListarAlunosResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        readGroup.MapGet("/{id}", async (
            Guid id,
            [FromServices] ObterAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
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

        readGroup.MapGet("/{alunoId}/treinos", async (
            Guid alunoId,
            [FromServices] ListarTreinosHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : Math.Clamp(tamanhoPagina, 1, 100);

            var query = new ListarTreinosQuery(alunoId, p, tp);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Lista treinos de um aluno com paginação")
        .Produces<ListarTreinosResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        writeGroup.MapPatch("/{id}", async (
            Guid id,
            [FromBody] AtualizarAlunoRequest request,
            [FromServices] AtualizarAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AtualizarAlunoCommand(id, request.Nome, request.Email, request.Telefone);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
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

        writeGroup.MapPatch("/{id}/status", async (
            Guid id,
            [FromBody] AlterarStatusAlunoRequest request,
            [FromServices] AlterarStatusAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AlterarStatusAlunoCommand(id, request.Status);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .RequireAuthorization("SystemAdmin")
        .WithSummary("Altera o status de um aluno (somente Admin)")
        .Produces<AlunoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }
}

public record AtualizarAlunoRequest(string? Nome, string? Email, string? Telefone);
public record AlterarStatusAlunoRequest(AlunoStatus Status);
