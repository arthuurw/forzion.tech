using forzion.tech.Api.Extensions;
using forzion.tech.Application.Auth;
using forzion.tech.Application.UseCases.Auth.Mfa;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Auth;

public static class MfaLoginEndpoints
{
    public static IEndpointRouteBuilder MapMfaLoginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth/mfa")
            .WithTags("Auth")
            .RequireAuthorization(MfaScopes.PolicyPendente)
            .RequireRateLimiting("mfa");

        group.MapPost("/verificar", async (
            CompletarLoginMfaRequest request,
            HttpContext http,
            [FromServices] CompletarLoginMfaHandler handler,
            CancellationToken cancellationToken) =>
        {
            var rotulo = http.Request.Headers.UserAgent.ToString();
            var result = await handler
                .HandleAsync(new CompletarLoginMfaCommand(request.Codigo, request.Fator, request.LembrarDispositivo, rotulo), cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Conclui o login verificando o segundo fator (TOTP, código de recuperação ou e-mail)")
        .Produces<CompletarLoginMfaResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/email/enviar", async (
            [FromServices] SolicitarCodigoLoginEmailHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok();
        })
        .WithSummary("Envia o código de login por e-mail para a conta com login pendente")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}

public record CompletarLoginMfaRequest(string Codigo, MfaFator Fator, bool LembrarDispositivo = false);
