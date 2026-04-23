using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Conta;

public static class ContaEndpoints
{
    public static IEndpointRouteBuilder MapContaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/conta").WithTags("Conta").RequireAuthorization();

        group.MapGet("/perfil", async (
            [FromServices] ObterPerfilHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        group.MapPatch("/perfil", async (
            [FromBody] AtualizarPerfilRequest request,
            [FromServices] AtualizarPerfilHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new AtualizarPerfilCommand(request.Nome), cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapPost("/senha", async (
            [FromBody] AlterarSenhaRequest request,
            [FromServices] AlterarSenhaHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new AlterarSenhaCommand(request.SenhaAtual, request.NovaSenha), cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        return endpoints;
    }
}

public record AtualizarPerfilRequest(string Nome);
public record AlterarSenhaRequest(string SenhaAtual, string NovaSenha);
