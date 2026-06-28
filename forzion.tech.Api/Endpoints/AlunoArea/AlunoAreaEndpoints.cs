using forzion.tech.Api.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.AtualizarAnamneseAluno;
using forzion.tech.Application.UseCases.Alunos.Dashboard;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;
using forzion.tech.Application.UseCases.AssinaturaAlunos;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarMinhaAssinaturaAluno;
using forzion.tech.Application.UseCases.AssinaturaAlunos.ObterAssinaturaAluno;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.AlunoArea;

public static class AlunoAreaEndpoints
{
    public static IEndpointRouteBuilder MapAlunoAreaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/aluno").WithTags("Aluno").RequireAuthorization("Aluno")
            .RequireRateLimiting("write")
            .AddEndpointFilter<PaginacaoFilter>();

        group.MapGet("/assinatura", async (
            [FromServices] ObterAssinaturaAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(userContext.PerfilId, cancellationToken);
            return result is null ? Results.NoContent() : Results.Ok(result);
        })
        .WithSummary("Retorna a assinatura ativa do aluno autenticado")
        .Produces<AssinaturaAlunoResponse>()
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/assinatura/cancelar", async (
            [FromServices] CancelarMinhaAssinaturaAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelarMinhaAssinaturaAlunoCommand(userContext.PerfilId), cancellationToken);

            if (result.IsSuccess) return Results.Ok();

            if (result.Error?.Code == CancelarMinhaAssinaturaAlunoHandler.AssinaturaNaoEncontradaErrorCode)
                return Results.NotFound(new { detail = result.Error.Message });

            return result.ToProblemResult();
        })
        .WithSummary("Aluno autenticado cancela a própria assinatura ativa")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

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

        group.MapGet("/dashboard", async (
            [FromServices] ObterAlunoDashboardHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .RequireRateLimiting("read")
        .WithSummary("Retorna o agregado do dashboard do aluno autenticado")
        .Produces<ObterAlunoDashboardResponse>();

        group.MapPost("/troca-treinador", async (
            [FromBody] SolicitarTrocaTreinadorRequest request,
            [FromServices] SolicitarTrocaTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new SolicitarTrocaTreinadorCommand(userContext.PerfilId, request.NovoTreinadorId, request.PacoteId), cancellationToken);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/aluno/vinculo", result.Value);
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
            var tp = tamanhoPagina < 1 ? 20 : Math.Clamp(tamanhoPagina, 1, 100);

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
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
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
            var tp = tamanhoPagina < 1 ? 20 : Math.Clamp(tamanhoPagina, 1, 100);

            var result = await handler.HandleAsync(userContext.PerfilId, p, tp, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista execuções de treino do aluno com paginação")
        .Produces<ListarExecucoesAlunoResponse>();

        group.MapGet("/progressao", async (
            [FromServices] ObterMinhaProgressaoHandler handler,
            [FromServices] TimeProvider timeProvider,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hoje = timeProvider.GetUtcNow().UtcDateTime.Date;
            var de = DateTime.TryParse(httpContext.Request.Query["de"], System.Globalization.CultureInfo.InvariantCulture, out var deParsed)
                ? deParsed.Date
                : hoje.AddDays(-90);
            var ate = DateTime.TryParse(httpContext.Request.Query["ate"], System.Globalization.CultureInfo.InvariantCulture, out var ateParsed)
                ? ateParsed.Date
                : hoje;
            if (de > ate)
                return Results.Problem(detail: "O parâmetro 'de' deve ser anterior a 'ate'.", statusCode: 400);
            var result = await handler.HandleAsync(de, ate, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Retorna a progressão de carga por exercício do aluno autenticado no período")
        .Produces<ProgressaoAlunoResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/execucoes", async (
            [FromBody] RegistrarExecucaoAlunoRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromServices] RegistrarExecucaoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            string? idempotencyNormalizada = null;
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                if (!Guid.TryParse(idempotencyKey, out var parsed))
                    return Results.Problem("Idempotency-Key deve ser um GUID válido.", statusCode: StatusCodes.Status400BadRequest);
                idempotencyNormalizada = parsed.ToString();
            }

            var exercicios = request.Exercicios
                .Select(e => new RegistrarExecucaoItemCommand(
                    e.TreinoExercicioId, e.SeriesExecutadas, e.RepeticoesExecutadas, e.CargaExecutada, e.Observacao))
                .ToList();

            var command = new RegistrarExecucaoCommand(
                request.TreinoId,
                userContext.PerfilId,
                request.DataExecucao,
                request.Observacao,
                exercicios,
                idempotencyNormalizada);

            var result = await handler.HandleAsync(command, cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/aluno/execucoes/{result.Value.ExecucaoId}", result.Value);
        })
        .AddEndpointFilter<RequireAssinaturaAtivaFilter>()
        .WithSummary("Registra a execução de um treino pelo aluno")
        .Produces<RegistrarExecucaoResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/anamnese", async (
            [FromBody] AtualizarAnamneseRequest request,
            [FromServices] AtualizarAnamneseAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var command = new AtualizarAnamneseAlunoCommand(
                userContext.PerfilId,
                request.DiasDisponiveis,
                request.TempoDisponivelMinutos,
                request.Finalidade,
                request.FocoTreino,
                request.NivelCondicionamento,
                request.LimitacoesFisicas,
                request.Doencas,
                request.ObservacoesAdicionais,
                request.ConsentimentoDadosSaude,
                request.ConsentimentoDadosSaudeEm);

            var result = await handler.HandleAsync(command, cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Aluno autenticado edita a própria anamnese (LGPD art. 18 III)")
        .Produces<AlunoResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

public record AtualizarAnamneseRequest(
    int? DiasDisponiveis,
    int? TempoDisponivelMinutos,
    forzion.tech.Domain.Enums.FinalidadeTreino? Finalidade,
    string? FocoTreino,
    forzion.tech.Domain.Enums.NivelCondicionamento? NivelCondicionamento,
    string? LimitacoesFisicas,
    string? Doencas,
    string? ObservacoesAdicionais,
    bool ConsentimentoDadosSaude = false,
    DateTime? ConsentimentoDadosSaudeEm = null);

public record SolicitarTrocaTreinadorRequest(Guid NovoTreinadorId, Guid PacoteId);

public record RegistrarExecucaoAlunoItemRequest(
    Guid TreinoExercicioId, int SeriesExecutadas, int RepeticoesExecutadas,
    decimal? CargaExecutada, string? Observacao);

public record RegistrarExecucaoAlunoRequest(
    Guid TreinoId, DateTime DataExecucao, string? Observacao,
    List<RegistrarExecucaoAlunoItemRequest> Exercicios);
