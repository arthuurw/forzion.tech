using forzion.tech.Api.Extensions;
using forzion.tech.Application.UseCases.Suporte.EnviarMensagem;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Suporte;

public static class SuporteEndpoints
{
    public static IEndpointRouteBuilder MapSuporteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/suporte").WithTags("Suporte").RequireAuthorization().RequireRateLimiting("write");

        group.MapPost("/mensagens", async (
            [FromBody] EnviarMensagemSuporteRequest request,
            [FromServices] EnviarMensagemSuporteHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler
                .HandleAsync(new EnviarMensagemSuporteCommand(request.Categoria, request.Assunto, request.Descricao), cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            // 202: ticket persistido na request; e-mail ao suporte é despachado via outbox (assíncrono).
            return Results.Accepted();
        })
        .WithSummary("Envia uma mensagem ao suporte (identidade do remetente vem do token, não do payload)")
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }
}

public record EnviarMensagemSuporteRequest(string Categoria, string Assunto, string Descricao);
