using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Application.UseCases.Treinos.DuplicarTreino;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Treinos.RemoverExercicio;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Treinos;

public static class TreinoEndpoints
{
    public static void MapTreinoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/treinos").WithTags("Treinos");

        group.MapPost("", async (
            CriarTreinoRequest request,
            CriarTreinoHandler handler,
            IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var treinadorId = ObterSupabaseId(httpContext);
            if (treinadorId is null)
                return Results.Unauthorized();

            var command = new CriarTreinoCommand(
                userContext.PerfilId, treinadorId.Value, request.AlunoId, request.Nome, request.Objetivo);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/treinos/{response.TreinoId}", response);
        })
        .RequireAuthorization()
        .WithSummary("Cria um novo treino para um aluno")
        .Produces<TreinoResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id}", async (
            Guid id,
            ObterTreinoHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var query = new ObterTreinoQuery(userContext.PerfilId, id);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Retorna os dados de um treino")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        app.MapGet("/alunos/{alunoId}/treinos", async (
            Guid alunoId,
            ListarTreinosHandler handler,
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

            var query = new ListarTreinosQuery(userContext.PerfilId, alunoId, p, tp);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithTags("Alunos")
        .WithSummary("Lista treinos de um aluno com paginação")
        .Produces<ListarTreinosResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/{id}/exercicios", async (
            Guid id,
            AdicionarExercicioRequest request,
            AdicionarExercicioHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var command = new AdicionarExercicioCommand(
                userContext.PerfilId, id, request.ExercicioId, request.Series, request.Repeticoes, request.Carga, request.Descanso);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Adiciona um exercício ao treino")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapDelete("/{id}/exercicios/{treinoExercicioId}", async (
            Guid id,
            Guid treinoExercicioId,
            RemoverExercicioHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var command = new RemoverExercicioCommand(userContext.PerfilId, id, treinoExercicioId);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Remove um exercício do treino")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/{id}/duplicar", async (
            Guid id,
            DuplicarTreinoHandler handler,
            IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var treinadorId = ObterSupabaseId(httpContext);
            if (treinadorId is null)
                return Results.Unauthorized();

            var command = new DuplicarTreinoCommand(userContext.PerfilId, treinadorId.Value, id);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Duplica um treino (gera uma cópia)")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/{id}/execucoes", async (
            Guid id,
            RegistrarExecucaoRequest request,
            RegistrarExecucaoHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var command = new RegistrarExecucaoCommand(
                userContext.PerfilId, 
                id, 
                request.AlunoId, 
                request.DataExecucao, 
                request.Observacao, 
                request.Exercicios.Select(e => new RegistrarExecucaoItemCommand(e.TreinoExercicioId, e.SeriesExecutadas, e.RepeticoesExecutadas, e.CargaExecutada, e.Observacao)).ToList());
            
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Registra a execução de um treino")
        .Produces<RegistrarExecucaoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static Guid? ObterSupabaseId(HttpContext context)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public record CriarTreinoRequest(Guid AlunoId, string Nome, ObjetivoTreino Objetivo);
public record AdicionarExercicioRequest(Guid ExercicioId, int Series, int Repeticoes, decimal? Carga, int? Descanso);
public record RegistrarExecucaoItemRequest(Guid TreinoExercicioId, int SeriesExecutadas, int RepeticoesExecutadas, decimal? CargaExecutada, string? Observacao);
public record RegistrarExecucaoRequest(Guid AlunoId, DateTime DataExecucao, string? Observacao, List<RegistrarExecucaoItemRequest> Exercicios);
