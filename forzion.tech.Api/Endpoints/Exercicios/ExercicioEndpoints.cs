using forzion.tech.Api.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Exercicios;

public static class ExercicioEndpoints
{
    public static void MapExercicioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/exercicios").WithTags("Exercícios")
            .AddEndpointFilter<PaginacaoFilter>();

        group.MapPost("", async (
            [FromBody] CriarExercicioRequest request,
            [FromServices] CriarExercicioHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var command = new CriarExercicioCommand(
                userContext.PerfilId, request.Nome, request.GrupoMuscularId, request.Descricao);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/exercicios/{result.Value.ExercicioId}", result.Value);
        })
        .RequireAuthorization()
        .WithSummary("Cadastra um novo exercício")
        .Produces<ExercicioResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("", async (
            [FromServices] ListarExerciciosHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (userContext.PerfilId == Guid.Empty)
                return Results.Unauthorized();

            var pagination = httpContext.ObterPaginacaoDoQuery();
            var query = new ListarExerciciosQuery(userContext.PerfilId, pagination.Pagina, pagination.TamanhoPagina);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Lista exercícios do treinador com paginação")
        .Produces<ListarExerciciosResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }
}

public record CriarExercicioRequest(string Nome, Guid GrupoMuscularId, string? Descricao);
