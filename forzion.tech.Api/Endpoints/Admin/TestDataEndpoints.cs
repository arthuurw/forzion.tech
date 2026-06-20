using forzion.tech.Api.Extensions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.TestData;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Admin;

public static class TestDataEndpoints
{
    public static IEndpointRouteBuilder MapTestDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/test-data").WithTags("Admin · Test Data")
            .RequireAuthorization("SystemAdmin")
            .RequireRateLimiting("write");

        group.MapGet("/contas", async (
            [FromServices] ListarContasTesteHandler handler,
            CancellationToken cancellationToken) =>
        {
            var contas = await handler.HandleAsync(cancellationToken);
            return Results.Ok(contas);
        })
        .WithSummary("Lista contas de teste (@e2e.test). Rota ausente em Production.")
        .Produces<IReadOnlyList<ContaTesteResumo>>();

        group.MapDelete("/contas/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirContaTesteHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ExcluirContaTesteCommand(id), cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Hard-delete de uma conta de teste (@e2e.test). Rota ausente em Production.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}
