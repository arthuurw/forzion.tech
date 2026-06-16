using forzion.tech.Api.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Application.UseCases.Treinos.AtualizarObservacaoExercicio;
using forzion.tech.Application.UseCases.Treinos.AtualizarTreino;
using forzion.tech.Application.UseCases.Treinos.EditarExercicioTreino;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Application.UseCases.Treinos.DuplicarTreino;
using forzion.tech.Application.UseCases.Treinos.ExcluirTreino;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Application.UseCases.Treinos.ListarAlunosTreino;
using forzion.tech.Application.UseCases.Treinos.RemoverExercicio;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Treinos;

public static class TreinoEndpoints
{
    public static void MapTreinoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/treinos")
            .WithTags("Treinos")
            .RequireAuthorization("Treinador")
            .RequireRateLimiting("write")
            .AddEndpointFilter<PerfilIdRequiredFilter>()
            .AddEndpointFilter<PaginacaoFilter>();

        group.MapPost("", async (
            [FromBody] CriarTreinoRequest request,
            [FromServices] CriarTreinoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var command = new CriarTreinoCommand(
                userContext.PerfilId, request.AlunoId, request.Nome, request.Objetivo,
                request.Dificuldade, request.DataInicio, request.DataFim);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/treinos/{result.Value.TreinoId}", result.Value);
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
            [FromServices] ObterTreinoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var query = new ObterTreinoQuery(id);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Retorna os dados de um treino")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{id}", async (
            Guid id,
            [FromBody] AtualizarTreinoRequest request,
            [FromServices] AtualizarTreinoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AtualizarTreinoCommand(id, request.Nome, request.Objetivo,
                request.Dificuldade, request.DataInicio, request.DataFim,
                request.LimparDataInicio, request.LimparDataFim);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithSummary("Atualiza nome/objetivo de um treino")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id}", async (
            Guid id,
            [FromServices] ExcluirTreinoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ExcluirTreinoCommand(id);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithSummary("Exclui um treino (apenas se nunca executado)")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id}/alunos", async (
            Guid id,
            [FromServices] ListarAlunosTreinoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler
                .HandleAsync(new ListarAlunosTreinoCommand(id), cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithSummary("Lista alunos com esta ficha vinculada (ativos)")
        .Produces<IReadOnlyList<TreinoAlunoVinculado>>(StatusCodes.Status200OK);

        group.MapPost("/{id}/vincular-aluno", async (
            Guid id,
            [FromBody] VincularFichaRequest request,
            [FromServices] VincularFichaAoAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new VincularFichaAoAlunoCommand(id, request.AlunoId);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithSummary("Vincula uma ficha de treino a um aluno")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id}/exercicios", async (
            Guid id,
            [FromBody] AdicionarExercicioRequest request,
            [FromServices] AdicionarExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AdicionarExercicioCommand(
                id,
                request.ExercicioId,
                request.Series.Select(s => new SerieConfigCommand(
                    s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso)).ToList());
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithSummary("Adiciona um exercício ao treino")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPut("/{id}/exercicios/{treinoExercicioId}", async (
            Guid id,
            Guid treinoExercicioId,
            [FromBody] EditarExercicioTreinoRequest request,
            [FromServices] EditarExercicioTreinoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new EditarExercicioTreinoCommand(
                id,
                treinoExercicioId,
                request.Series.Select(s => new SerieConfigEditCommand(
                    s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso)).ToList());
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithSummary("Edita as séries de um exercício na ficha")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapPatch("/{id}/exercicios/{treinoExercicioId}/observacao", async (
            Guid id,
            Guid treinoExercicioId,
            [FromBody] AtualizarObservacaoExercicioRequest request,
            [FromServices] AtualizarObservacaoExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AtualizarObservacaoExercicioCommand(id, treinoExercicioId, request.Observacao);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithSummary("Atualiza a observação de um exercício na ficha")
        .Produces<TreinoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id}/exercicios/{treinoExercicioId}", async (
            Guid id,
            Guid treinoExercicioId,
            [FromServices] RemoverExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RemoverExercicioCommand(id, treinoExercicioId);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
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
            [FromServices] DuplicarTreinoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var command = new DuplicarTreinoCommand(userContext.PerfilId, id);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/treinos/{result.Value.TreinoId}", result.Value);
        })
        .RequireAuthorization()
        .WithSummary("Duplica um treino (gera uma cópia)")
        .Produces<TreinoResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

}

public record EditarExercicioTreinoRequest(IReadOnlyList<SerieConfigRequest> Series);
public record AtualizarObservacaoExercicioRequest(string? Observacao);
public record CriarTreinoRequest(Guid? AlunoId, string Nome, ObjetivoTreino Objetivo, DificuldadeTreino Dificuldade = DificuldadeTreino.Iniciante, DateOnly? DataInicio = null, DateOnly? DataFim = null);
public record AtualizarTreinoRequest(string? Nome, ObjetivoTreino? Objetivo, DificuldadeTreino? Dificuldade = null, DateOnly? DataInicio = null, DateOnly? DataFim = null, bool LimparDataInicio = false, bool LimparDataFim = false);
public record SerieConfigRequest(int Quantidade, int RepeticoesMin, int? RepeticoesMax, string? Descricao, decimal? Carga, int? Descanso);
public record AdicionarExercicioRequest(Guid ExercicioId, IReadOnlyList<SerieConfigRequest> Series);
