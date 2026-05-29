using forzion.tech.Api.Extensions;
using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Application.UseCases.Conta.Logout;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Conta;

public static class ContaEndpoints
{
    public static IEndpointRouteBuilder MapContaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/conta").WithTags("Conta").RequireAuthorization().RequireRateLimiting("write");

        group.MapGet("/perfil", async (
            [FromServices] ObterPerfilHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Retorna o perfil do usuário autenticado")
        .Produces<PerfilResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/perfil", async (
            [FromBody] AtualizarPerfilRequest request,
            [FromServices] AtualizarPerfilHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AtualizarPerfilCommand(request.Nome), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Atualiza o nome do usuário autenticado")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/senha", async (
            [FromBody] AlterarSenhaRequest request,
            [FromServices] AlterarSenhaHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AlterarSenhaCommand(request.SenhaAtual, request.NovaSenha), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Altera a senha do usuário autenticado")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/logout", async (
            [FromServices] LogoutHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Revoga o token atual e faz logout")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }
}

public record AtualizarPerfilRequest(string Nome);
public record AlterarSenhaRequest(string SenhaAtual, string NovaSenha);
