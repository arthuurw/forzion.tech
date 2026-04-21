using forzion.tech.Api.Extensions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Admin.GruposMusculares;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.ExcluirPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;
using forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadores;
using forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin").WithTags("Admin").RequireAuthorization("SystemAdmin");

        group.MapGet("/treinadores", async (
            [FromServices] ListarTreinadoresHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = Enum.TryParse<TreinadorStatus>(httpContext.Request.Query["status"], out var status);
            var hasStatus = httpContext.Request.Query.ContainsKey("status");
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;

            var result = await handler.HandleAsync(hasStatus ? status : null, p, tp, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista treinadores com filtro opcional por status")
        .Produces<ListarTreinadoresResponse>();

        group.MapPost("/treinadores/{id:guid}/aprovar", async (
            Guid id,
            [FromBody] AprovarTreinadorRequest request,
            [FromServices] AprovarTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AprovarTreinadorCommand(id, userContext.ContaId, request.Observacao), cancellationToken);

            return Results.Ok(result);
        })
        .WithSummary("Aprova um treinador")
        .Produces<TreinadorResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/treinadores/{id:guid}/reprovar", async (
            Guid id,
            [FromBody] ReprovarTreinadorRequest request,
            [FromServices] ReprovarTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(
                new ReprovarTreinadorCommand(id, userContext.ContaId, request.Observacao), cancellationToken);

            return Results.NoContent();
        })
        .WithSummary("Reprova um treinador aguardando aprovação")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/treinadores/{id:guid}/inativar", async (
            Guid id,
            [FromBody] InativarTreinadorRequest request,
            [FromServices] InativarTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(
                new InativarTreinadorCommand(id, userContext.ContaId, request.Observacao), cancellationToken);

            return Results.NoContent();
        })
        .WithSummary("Inativa um treinador e faz cascade nos vínculos")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/treinadores/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ExcluirTreinadorCommand(id, userContext.ContaId), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Exclui permanentemente um treinador inativo e todas as suas dependências. LogAprovacao é preservado.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPatch("/treinadores/{id:guid}/plano", async (
            Guid id,
            [FromBody] AtribuirPlanoRequest request,
            [FromServices] AtribuirPlanoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtribuirPlanoCommand(id, request.PlanoId, userContext.ContaId), cancellationToken);

            return Results.Ok(result);
        })
        .WithSummary("Atribui um PlanoTreinador a um treinador")
        .Produces<TreinadorResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/planos", async (
            [FromServices] ListarPlanosTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista todos os planos de treinador")
        .Produces<IReadOnlyList<PlanoTreinadorResponse>>();

        group.MapPost("/planos", async (
            [FromBody] CriarPlanoTreinadorRequest request,
            [FromServices] CriarPlanoTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarPlanoTreinadorCommand(request.Nome, request.MaxAlunos, request.Preco), cancellationToken);

            return Results.Created($"/admin/planos/{result.PlanoId}", result);
        })
        .WithSummary("Cria um novo plano de treinador")
        .Produces<PlanoTreinadorResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/planos/{id:guid}", async (
            Guid id,
            [FromBody] AtualizarPlanoTreinadorRequest request,
            [FromServices] AtualizarPlanoTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarPlanoTreinadorCommand(id, request.Nome, request.MaxAlunos, request.Preco), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Atualiza nome, maxAlunos e/ou preço de um plano")
        .Produces<PlanoTreinadorResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/planos/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirPlanoTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ExcluirPlanoTreinadorCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Inativa um plano de treinador")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Grupos Musculares
        group.MapGet("/grupos-musculares", async (
            [FromServices] ListarGruposMuscularesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista todos os grupos musculares")
        .Produces<IReadOnlyList<GrupoMuscularResponse>>();

        group.MapPost("/grupos-musculares", async (
            [FromBody] CriarGrupoMuscularRequest request,
            [FromServices] CriarGrupoMuscularHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new CriarGrupoMuscularCommand(request.Nome), cancellationToken);
            return Results.Created($"/admin/grupos-musculares/{result.Id}", result);
        })
        .WithSummary("Cria um novo grupo muscular")
        .Produces<GrupoMuscularResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/grupos-musculares/{id:guid}", async (
            Guid id,
            [FromBody] AtualizarGrupoMuscularRequest request,
            [FromServices] AtualizarGrupoMuscularHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AtualizarGrupoMuscularCommand(id, request.Nome), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Atualiza um grupo muscular")
        .Produces<GrupoMuscularResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/grupos-musculares/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirGrupoMuscularHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ExcluirGrupoMuscularCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Exclui um grupo muscular")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/exercicios", async (
            [FromServices] ListarExerciciosHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var q = httpContext.Request.Query;
            _ = Enum.TryParse<forzion.tech.Domain.Enums.GrupoMuscular>(q["grupoMuscular"], out var grupo);
            var hasGrupo = q.ContainsKey("grupoMuscular");
            var nome = q["nome"].ToString();
            var ordenarPor = q["ordenarPor"].ToString();
            var result = await handler.HandleAsync(
                new ListarExerciciosQuery(null, pagination.Pagina, pagination.TamanhoPagina,
                    string.IsNullOrEmpty(nome) ? null : nome,
                    hasGrupo ? grupo : null,
                    string.IsNullOrEmpty(ordenarPor) ? "nome" : ordenarPor),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista exercícios da biblioteca global")
        .Produces<ListarExerciciosResponse>();

        group.MapPost("/exercicios", async (
            [FromBody] CriarExercicioGlobalRequest request,
            [FromServices] CriarExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarExercicioCommand(null, request.Nome, request.GrupoMuscular, request.Descricao),
                cancellationToken);
            return Results.Created($"/admin/exercicios/{result.ExercicioId}", result);
        })
        .WithSummary("Cria exercício na biblioteca global (TreinadorId = null)")
        .Produces<ExercicioResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/exercicios/{id:guid}", async (
            Guid id,
            [FromBody] AtualizarExercicioGlobalRequest request,
            [FromServices] AtualizarExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarExercicioCommand(id, null, request.Nome, request.GrupoMuscular, request.Descricao),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Atualiza exercício global")
        .Produces<ExercicioResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/exercicios/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ExcluirExercicioCommand(id, null), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Exclui exercício global")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}

public record AprovarTreinadorRequest(string? Observacao = null);
public record ReprovarTreinadorRequest(string? Observacao = null);
public record InativarTreinadorRequest(string? Observacao = null);
public record AtribuirPlanoRequest(Guid PlanoId);
public record CriarPlanoTreinadorRequest(string Nome, int MaxAlunos, decimal Preco);
public record AtualizarPlanoTreinadorRequest(string? Nome, int? MaxAlunos, decimal? Preco);
public record CriarGrupoMuscularRequest(string Nome);
public record AtualizarGrupoMuscularRequest(string Nome);
public record CriarExercicioGlobalRequest(string Nome, forzion.tech.Domain.Enums.GrupoMuscular GrupoMuscular, string? Descricao);
public record AtualizarExercicioGlobalRequest(string? Nome, forzion.tech.Domain.Enums.GrupoMuscular? GrupoMuscular, string? Descricao);
