using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin").WithTags("Admin").RequireAuthorization("SystemAdmin");

        group.MapPost("/treinadores/{id:guid}/aprovar", async (
            Guid id,
            [FromBody] AprovarTreinadorRequest request,
            AprovarTreinadorHandler handler,
            IUserContext userContext,
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

        group.MapPost("/treinadores/{id:guid}/inativar", async (
            Guid id,
            [FromBody] InativarTreinadorRequest request,
            InativarTreinadorHandler handler,
            IUserContext userContext,
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

        group.MapPatch("/treinadores/{id:guid}/plano", async (
            Guid id,
            [FromBody] AtribuirPlanoRequest request,
            AtribuirPlanoHandler handler,
            IUserContext userContext,
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
            ListarPlanosTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista todos os planos de treinador")
        .Produces<IReadOnlyList<PlanoTreinadorResponse>>();

        group.MapPost("/planos", async (
            [FromBody] CriarPlanoTreinadorRequest request,
            CriarPlanoTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarPlanoTreinadorCommand(request.Nome, request.MaxAlunos, request.Preco), cancellationToken);

            return Results.Created($"/admin/planos/{result.PlanoId}", result);
        })
        .WithSummary("Cria um novo plano de treinador")
        .Produces<PlanoTreinadorResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}

public record AprovarTreinadorRequest(string? Observacao = null);
public record InativarTreinadorRequest(string? Observacao = null);
public record AtribuirPlanoRequest(Guid PlanoId);
public record CriarPlanoTreinadorRequest(string Nome, int MaxAlunos, decimal Preco);
