using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;

namespace forzion.tech.Api.Endpoints.AlunoArea;

public static class AlunoAreaEndpoints
{
    public static IEndpointRouteBuilder MapAlunoAreaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/aluno").WithTags("Aluno").RequireAuthorization("Aluno");

        group.MapGet("/fichas", async (
            ListarFichasAlunoHandler handler,
            IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(userContext.PerfilId, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista fichas de treino ativas do aluno")
        .Produces<IReadOnlyList<FichaAlunoResponse>>();

        group.MapGet("/execucoes", async (
            ListarExecucoesAlunoHandler handler,
            IUserContext userContext,
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

        group.MapPost("/execucoes", async (
            RegistrarExecucaoAlunoRequest request,
            RegistrarExecucaoHandler handler,
            IUserContext userContext,
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

public record RegistrarExecucaoAlunoItemRequest(
    Guid TreinoExercicioId, int SeriesExecutadas, int RepeticoesExecutadas,
    decimal? CargaExecutada, string? Observacao);

public record RegistrarExecucaoAlunoRequest(
    Guid TreinoId, DateTime DataExecucao, string? Observacao,
    List<RegistrarExecucaoAlunoItemRequest> Exercicios);
