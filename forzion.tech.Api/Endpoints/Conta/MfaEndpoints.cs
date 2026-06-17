using forzion.tech.Api.Extensions;
using forzion.tech.Application.UseCases.Conta.Mfa;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Conta;

public static class MfaEndpoints
{
    public static IEndpointRouteBuilder MapMfaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/conta/mfa")
            .WithTags("Conta")
            .RequireAuthorization()
            .RequireRateLimiting("auth");

        group.MapPost("/totp/iniciar", async (
            [FromServices] IniciarEnrollTotpHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Inicia a configuração do TOTP e retorna o segredo e o URI otpauth")
        .Produces<IniciarEnrollTotpResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/totp/confirmar", async (
            [FromBody] ConfirmarMfaTotpRequest request,
            [FromServices] ConfirmarEnrollTotpHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler
                .HandleAsync(new ConfirmarEnrollTotpCommand(request.Codigo), cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Confirma o TOTP com um código válido, habilita o MFA e retorna os códigos de recuperação")
        .Produces<ConfirmarEnrollTotpResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }
}

public record ConfirmarMfaTotpRequest(string Codigo);
