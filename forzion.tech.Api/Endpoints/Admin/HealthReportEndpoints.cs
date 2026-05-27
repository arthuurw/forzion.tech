using forzion.tech.Application.UseCases.Admin.HealthReport;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Admin;

public static class HealthReportEndpoints
{
    public static IEndpointRouteBuilder MapHealthReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/health-report").WithTags("Admin")
            .RequireAuthorization("SystemAdmin")
            .RequireRateLimiting("write");

        group.MapGet("/config", async (
            [FromServices] ObterHealthReportConfigHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result is null ? Results.NoContent() : Results.Ok(result);
        })
        .WithSummary("Obtém a configuração do relatório de saúde")
        .Produces<HealthReportConfigResponse>()
        .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/config", async (
            [FromBody] AtualizarHealthReportConfigRequest request,
            [FromServices] AtualizarHealthReportConfigHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarHealthReportConfigCommand(
                    request.Ativo,
                    request.HoraEnvioUtc,
                    request.Destinatarios ?? Array.Empty<string>(),
                    request.IncluirLiveness,
                    request.IncluirKpis,
                    request.IncluirEntregabilidade,
                    request.IncluirErros),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Cria ou atualiza a configuração do relatório de saúde")
        .Produces<HealthReportConfigResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/snapshots", async (
            [FromServices] ListarHealthSnapshotsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            _ = int.TryParse(httpContext.Request.Query["limite"], out var limite);
            var result = await handler.HandleAsync(limite > 0 ? limite : null, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista os snapshots de saúde mais recentes")
        .Produces<IReadOnlyList<HealthSnapshotResponse>>();

        group.MapPost("/run", async (
            [FromServices] ExecutarRelatorioSaudeHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Executa o relatório de saúde imediatamente")
        .Produces<HealthSnapshotResponse>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}

public record AtualizarHealthReportConfigRequest(
    bool Ativo,
    TimeOnly HoraEnvioUtc,
    IReadOnlyList<string>? Destinatarios,
    bool IncluirLiveness,
    bool IncluirKpis,
    bool IncluirEntregabilidade,
    bool IncluirErros);
