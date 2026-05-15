using forzion.tech.Api.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;
using forzion.tech.Application.UseCases.Exercicios.CopiarExercicioGlobal;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacoteAluno;
using forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;
using forzion.tech.Application.UseCases.Pacotes.ExcluirPacoteAluno;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;
using forzion.tech.Application.UseCases.Treinos.ListarFichasDoAluno;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Application.UseCases.Vinculos.ReativarVinculo;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Treinador;

public static class TreinadorEndpoints
{
    public static IEndpointRouteBuilder MapTreinadorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/treinador").WithTags("Treinador").RequireAuthorization("Treinador").RequireRateLimiting("write")
            .AddEndpointFilter<PaginacaoFilter>();

        // --- Vínculos ---

        group.MapPost("/vinculos/{id:guid}/aprovar", async (
            Guid id,
            [FromBody] AprovarVinculoRequest request,
            [FromServices] AprovarVinculoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AprovarVinculoCommand(id, userContext.PerfilId, request.PacoteAlunoId, request.TrarFichas), cancellationToken);

            return Results.Ok(result);
        })
        .WithSummary("Aprova o vínculo de um aluno ao treinador")
        .Produces<VinculoResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/vinculos/{id:guid}/desvincular", async (
            Guid id,
            [FromBody] DesvincularAlunoRequest request,
            [FromServices] DesvincularAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(
                new DesvincularAlunoCommand(id, userContext.PerfilId, request.Observacao), cancellationToken);

            return Results.NoContent();
        })
        .WithSummary("Desvincula um aluno do treinador")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/alunos/{alunoId:guid}/reativar", async (
            Guid alunoId,
            [FromBody] ReativarVinculoRequest request,
            [FromServices] ReativarVinculoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ReativarVinculoCommand(userContext.PerfilId, alunoId, request.PacoteAlunoId), cancellationToken);

            return Results.Ok(result);
        })
        .WithSummary("Reativa um aluno inativo criando um novo vínculo aprovado")
        .Produces<VinculoResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // --- Alunos ---

        group.MapGet("/alunos", async (
            [FromServices] ListarAlunosHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;

            var query = new ListarAlunosQuery(userContext.PerfilId, p, tp);
            var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Lista alunos do treinador")
        .Produces<ListarAlunosResponse>();

        group.MapGet("/alunos/{alunoId:guid}", async (
            Guid alunoId,
            [FromServices] ObterAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ObterAlunoQuery(alunoId), cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Obtém os dados de um aluno vinculado ao treinador")
        .Produces<AlunoResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/alunos/{alunoId:guid}/fichas", async (
            Guid alunoId,
            [FromServices] ListarFichasDoAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(userContext.PerfilId, alunoId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Lista fichas ativas de um aluno para o treinador autenticado")
        .Produces<IReadOnlyList<TreinoAlunoResponse>>();

        group.MapGet("/alunos/{alunoId:guid}/progressao", async (
            Guid alunoId,
            [FromServices] ObterProgressaoAlunoHandler handler,
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

            var query = new ObterProgressaoAlunoQuery(alunoId, de, ate);
            var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Retorna a progressão de carga por exercício de um aluno no período")
        .Produces<ProgressaoAlunoResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/vinculos", async (
            [FromServices] ListarVinculosHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = Enum.TryParse<VinculoStatus>(httpContext.Request.Query["status"], out var status);
            var hasStatus = httpContext.Request.Query.ContainsKey("status");
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;

            var result = await handler.HandleAsync(
                userContext.PerfilId,
                hasStatus ? status : null,
                p,
                tp,
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithSummary("Lista vínculos do treinador com paginação")
        .Produces<ListarVinculosResponse>();

        // --- Treinos ---

        group.MapGet("/treinos", async (
            [FromServices] ListarTreinosDoTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;
            var nome = httpContext.Request.Query["nome"].FirstOrDefault();
            var objetivo = httpContext.Request.Query["objetivo"].FirstOrDefault();
            var ordenarPorRaw = httpContext.Request.Query["ordenarPor"].FirstOrDefault();

            var ordenacaoTreinosPermitida = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "nome", "objetivo", "nomeAluno", "createdAt" };
            var ordenarPor = !string.IsNullOrWhiteSpace(ordenarPorRaw) && ordenacaoTreinosPermitida.Contains(ordenarPorRaw)
                ? ordenarPorRaw
                : null;

            var result = await handler.HandleAsync(userContext.PerfilId, p, tp, nome, objetivo, ordenarPor, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Lista treinos do treinador")
        .Produces<ListarTreinosResponse>();

        group.MapPost("/alunos/{alunoId:guid}/fichas/{treinoId:guid}", async (
            Guid alunoId,
            Guid treinoId,
            [FromServices] VincularFichaAoAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new VincularFichaAoAlunoCommand(treinoId, alunoId), cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Vincula uma ficha de treino a um aluno")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // --- Grupos Musculares ---

        group.MapGet("/grupos-musculares", async (
            [FromServices] ListarGruposMuscularesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista todos os grupos musculares");

        // --- Exercícios ---

        group.MapGet("/exercicios", async (
            [FromServices] ListarExerciciosHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var q = httpContext.Request.Query;
            _ = int.TryParse(q["pagina"], out var pagina);
            _ = int.TryParse(q["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;
            var apenasGlobal = q["global"].ToString() == "true";
            _ = Enum.TryParse<forzion.tech.Domain.Enums.GrupoMuscular>(q["grupoMuscular"], out var grupo);
            var hasGrupo = q.ContainsKey("grupoMuscular");
            var nome = q["nome"].ToString();
            var ordenarPorRaw = q["ordenarPor"].ToString();

            var ordenacaoExerciciosPermitida = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "nome", "grupoMuscular" };
            var ordenarPor = ordenacaoExerciciosPermitida.Contains(ordenarPorRaw) ? ordenarPorRaw : "nome";

            // apenasGlobal=true → só globais; senão → próprios + globais
            Guid? treinadorId = apenasGlobal ? null : userContext.PerfilId;

            var query = new ListarExerciciosQuery(treinadorId, p, tp,
                string.IsNullOrEmpty(nome) ? null : nome,
                hasGrupo ? grupo : null,
                ordenarPor);

            var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Lista exercícios do treinador (próprios + globais). global=true retorna apenas globais.")
        .Produces<ListarExerciciosResponse>();

        group.MapPost("/exercicios", async (
            [FromBody] CriarExercicioTreinadorRequest request,
            [FromServices] CriarExercicioHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarExercicioCommand(userContext.PerfilId, request.Nome, request.GrupoMuscular, request.Descricao),
                cancellationToken);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/treinador/exercicios/{result.Value.ExercicioId}", result.Value);
        })
        .WithSummary("Cria um exercício na biblioteca do treinador")
        .Produces<ExercicioResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/exercicios/{id:guid}/copiar", async (
            Guid id,
            [FromServices] CopiarExercicioGlobalHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CopiarExercicioGlobalCommand(id, userContext.PerfilId), cancellationToken);

            return Results.Created($"/treinador/exercicios/{result.ExercicioId}", result);
        })
        .WithSummary("Copia um exercício global para a biblioteca do treinador")
        .Produces<ExercicioResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/exercicios/{id:guid}", async (
            Guid id,
            [FromBody] AtualizarExercicioTreinadorRequest request,
            [FromServices] AtualizarExercicioHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarExercicioCommand(id, userContext.PerfilId, request.Nome, request.GrupoMuscular, request.Descricao),
                cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Atualiza exercício da biblioteca do treinador")
        .Produces<ExercicioResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/exercicios/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirExercicioHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ExcluirExercicioCommand(id, userContext.PerfilId), cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Exclui exercício da biblioteca do treinador")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // --- Pacotes ---

        group.MapGet("/pacotes", async (
            [FromServices] ListarPacotesAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(userContext.PerfilId, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista pacotes criados pelo treinador")
        .Produces<IReadOnlyList<PacoteAlunoResponse>>();

        group.MapPost("/pacotes", async (
            [FromBody] CriarPacoteAlunoRequest request,
            [FromServices] CriarPacoteAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarPacoteAlunoCommand(userContext.PerfilId, request.Nome, request.Preco, request.Descricao),
                cancellationToken);

            return Results.Created($"/treinador/pacotes/{result.PacoteId}", result);
        })
        .WithSummary("Cria um novo pacote de fichas para alunos")
        .Produces<PacoteAlunoResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/pacotes/{pacoteId:guid}", async (
            Guid pacoteId,
            [FromBody] AtualizarPacoteAlunoRequest request,
            [FromServices] AtualizarPacoteAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarPacoteAlunoCommand(userContext.PerfilId, pacoteId, request.Nome, request.Preco, request.Descricao),
                cancellationToken);

            return Results.Ok(result);
        })
        .WithSummary("Atualiza um pacote de fichas do treinador")
        .Produces<PacoteAlunoResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/pacotes/{pacoteId:guid}", async (
            Guid pacoteId,
            [FromServices] ExcluirPacoteAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ExcluirPacoteAlunoCommand(userContext.PerfilId, pacoteId), cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Exclui um pacote do treinador")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}

public record AprovarVinculoRequest(Guid PacoteAlunoId, bool TrarFichas = false);
public record ReativarVinculoRequest(Guid PacoteAlunoId);
public record DesvincularAlunoRequest(string? Observacao = null);
public record CriarExercicioTreinadorRequest(string Nome, GrupoMuscular GrupoMuscular, string? Descricao = null);
public record AtualizarExercicioTreinadorRequest(string? Nome, GrupoMuscular? GrupoMuscular, string? Descricao);
public record CriarPacoteAlunoRequest(string Nome, decimal Preco, string? Descricao = null);
public record AtualizarPacoteAlunoRequest(string? Nome, decimal? Preco, string? Descricao);
