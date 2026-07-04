using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Notificacoes;

public static class NotificacoesEndpoints
{
    public static IEndpointRouteBuilder MapNotificacoesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/notificacoes").WithTags("Notificacoes").RequireAuthorization();

        group.MapGet("/", async (
            [FromServices] INotificacaoRepository repository,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : Math.Clamp(tamanhoPagina, 1, 100);

            var notificacoes = await repository
                .ListarPorContaAsync(userContext.ContaId, (p - 1) * tp, tp, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(notificacoes.Select(NotificacaoResponse.De).ToList());
        })
        .RequireRateLimiting("read")
        .WithSummary("Lista as notificações da conta autenticada (mais recentes primeiro, paginado)")
        .Produces<List<NotificacaoResponse>>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/nao-lidas/contador", async (
            [FromServices] INotificacaoRepository repository,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var total = await repository.ContarNaoLidasAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new ContadorNaoLidasResponse(total));
        })
        .RequireRateLimiting("read")
        .WithSummary("Retorna a quantidade de notificações não-lidas da conta autenticada")
        .Produces<ContadorNaoLidasResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:guid}/lida", async (
            Guid id,
            [FromServices] INotificacaoRepository repository,
            [FromServices] IUserContext userContext,
            [FromServices] TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var marcou = await repository
                .MarcarLidaAsync(id, userContext.ContaId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
                .ConfigureAwait(false);

            return marcou ? Results.NoContent() : Results.NotFound();
        })
        .RequireRateLimiting("write")
        .WithSummary("Marca uma notificação da conta autenticada como lida")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

public record NotificacaoResponse(
    Guid Id,
    TipoNotificacao Tipo,
    string Titulo,
    string Corpo,
    string? LinkRelativo,
    bool Lida,
    DateTime CreatedAt)
{
    public static NotificacaoResponse De(Notificacao n) =>
        new(n.Id, n.Tipo, n.Titulo, n.Corpo, n.LinkRelativo, n.Lida, n.CreatedAt);
}

public record ContadorNaoLidasResponse(int Total);
