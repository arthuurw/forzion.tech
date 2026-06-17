using forzion.tech.Api.Extensions;
using forzion.tech.Application.UseCases.Auth.StepUp;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Auth;

public static class StepUpEndpoints
{
    public static IEndpointRouteBuilder MapStepUpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth/step-up")
            .WithTags("Auth")
            .RequireAuthorization()
            .RequireRateLimiting("auth");

        group.MapPost("/iniciar", async (
            [FromServices] IniciarStepUpHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Inicia o step-up: usa TOTP se habilitado, senão envia código por e-mail")
        .Produces<IniciarStepUpResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/verificar", async (
            [FromBody] VerificarStepUpRequest request,
            [FromServices] VerificarStepUpHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler
                .HandleAsync(new VerificarStepUpCommand(request.Codigo), cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Verifica o fator (TOTP ou código de e-mail) e emite um token de step-up curto")
        .Produces<VerificarStepUpResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }
}

public record VerificarStepUpRequest(string Codigo);
