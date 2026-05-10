using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.AlunoArea;

public static class AlunoAreaEndpoints
{
    public static IEndpointRouteBuilder MapAlunoAreaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/aluno").WithTags("Aluno").RequireAuthorization("Aluno")
            .AddEndpointFilter<PaginacaoFilter>();

        group.MapGet("/vinculo", async (
            [FromServices] ObterVinculoAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(userContext.PerfilId, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Retorna o vínculo ativo e pendente do aluno autenticado")
        .Produces<ObterVinculoAlunoResponse>();

        group.MapPost("/troca-treinador", async (
            [FromBody] SolicitarTrocaTreinadorRequest request,
            [FromServices] SolicitarTrocaTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new SolicitarTrocaTreinadorCommand(userContext.PerfilId, request.NovoTreinadorId, request.PacoteId), cancellationToken);

            return Results.Created($"/aluno/vinculo", result);
        })
        .WithSummary("Solicita troca de treinador criando vínculo pendente com o novo treinador")
        .Produces<VinculoResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/fichas", async (
            [FromServices] ListarFichasAlunoHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;

            var result = await handler.HandleAsync(userContext.PerfilId, p, tp, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista fichas de treino ativas do aluno")
        .Produces<ListarFichasAlunoResponse>();

        group.MapGet("/fichas/{treinoAlunoId:guid}", async (
            Guid treinoAlunoId,
            [FromServices] ObterFichaAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(treinoAlunoId, userContext.PerfilId, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Obtém o detalhe de uma ficha vinculada ao aluno autenticado")
        .Produces<FichaAlunoDetalheResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/execucoes", async (
            [FromServices] ListarExecucoesAlunoHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;

            var result = await handler.HandleAsync(userContext.PerfilId, p, tp, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista execuções de treino do aluno com paginação")
        .Produces<ListarExecucoesAlunoResponse>();

        group.MapGet("/progressao", async (
            [FromServices] ObterMinhaProgressaoHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hoje = DateTime.UtcNow.Date;
            var de = DateTime.TryParse(httpContext.Request.Query["de"], out var deParsed)
                ? deParsed.Date
                : hoje.AddDays(-90);
            var ate = DateTime.TryParse(httpContext.Request.Query["ate"], out var ateParsed)
                ? ateParsed.Date
                : hoje;
            if (de > ate)
                return Results.BadRequest("O parâmetro 'de' deve ser anterior a 'ate'.");
            var result = await handler.HandleAsync(de, ate, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Retorna a progressão de carga por exercício do aluno autenticado no período")
        .Produces<ProgressaoAlunoResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/execucoes", async (
            [FromBody] RegistrarExecucaoAlunoRequest request,
            [FromServices] RegistrarExecucaoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var exercicios = request.Exercicios
                .Select(e => new RegistrarExecucaoItemCommand(
                    e.TreinoExercicioId, e.SeriesExecutadas, e.RepeticoesExecutadas, e.CargaExecutada, e.Observacao))
                .ToList();

            var command = new RegistrarExecucaoCommand(
                request.TreinoId,
                userContext.PerfilId,
                request.DataExecucao,
                request.Observacao,
                exercicios);

            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/aluno/execucoes/{result.ExecucaoId}", result);
        })
        .WithSummary("Registra a execução de um treino pelo aluno")
        .Produces<RegistrarExecucaoResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

public record SolicitarTrocaTreinadorRequest(Guid NovoTreinadorId, Guid PacoteId);

public record RegistrarExecucaoAlunoItemRequest(
    Guid TreinoExercicioId, int SeriesExecutadas, int RepeticoesExecutadas,
    decimal? CargaExecutada, string? Observacao);

public record RegistrarExecucaoAlunoRequest(
    Guid TreinoId, DateTime DataExecucao, string? Observacao,
    List<RegistrarExecucaoAlunoItemRequest> Exercicios);
