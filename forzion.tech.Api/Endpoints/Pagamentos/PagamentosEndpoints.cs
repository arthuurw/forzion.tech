using forzion.tech.Api.Extensions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
using forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace forzion.tech.Api.Endpoints.Pagamentos;

public static class PagamentosEndpoints
{
    public static IEndpointRouteBuilder MapPagamentosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var alunoGroup = endpoints.MapGroup("/aluno/pagamentos")
            .WithTags("Pagamentos")
            .RequireAuthorization("Aluno")
            .RequireRateLimiting("read");

        alunoGroup.MapGet("/{pagamentoId:guid}", async (
            Guid pagamentoId,
            [FromServices] ObterStatusPagamentoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ObterStatusPagamentoQuery(pagamentoId, userContext.PerfilId), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Obtém status e QR code de um pagamento")
        .Produces<PagamentoResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        alunoGroup.MapGet("/assinatura/{assinaturaId:guid}", async (
            Guid assinaturaId,
            [FromServices] ListarPagamentosAssinaturaAlunoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListarPagamentosAssinaturaAlunoQuery(assinaturaId, userContext.PerfilId), cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Lista histórico de pagamentos de uma assinatura")
        .Produces<IReadOnlyList<PagamentoResponse>>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        var treinadorGroup = endpoints.MapGroup("/treinador/pagamentos")
            .WithTags("Pagamentos")
            .RequireAuthorization("Treinador")
            .RequireRateLimiting("write");

        treinadorGroup.MapPost("/cobrar/{assinaturaId:guid}", async (
            Guid assinaturaId,
            [FromQuery] MetodoPagamento metodo,
            [FromServices] GerarCobrancaMensalHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GerarCobrancaMensalCommand(assinaturaId, userContext.PerfilId, metodo), cancellationToken).ConfigureAwait(false);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Gera cobrança mensal para uma assinatura (metodo: Pix ou Cartao)")
        .Produces<PagamentoResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/processar-renovacoes", async (
            HttpContext httpContext,
            [FromServices] GerarCobrancaMensalHandler gerarHandler,
            [FromServices] IAssinaturaAlunoRepository assinaturaRepository,
            [FromServices] ILogger<Program> logger,
            [FromServices] TimeProvider timeProvider,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var apiKey = configuration["Internal:ApiKey"];
            var headerKey = httpContext.Request.Headers["X-Internal-Key"].FirstOrDefault() ?? string.Empty;

            // Constant-time comparison para evitar timing attack na chave.
            // Verificar comprimento antes de FixedTimeEquals (que lança ArgumentException em spans de tamanhos diferentes).
            var headerBytes = Encoding.UTF8.GetBytes(headerKey);
            var keyBytes = Encoding.UTF8.GetBytes(apiKey ?? string.Empty);
            if (string.IsNullOrEmpty(apiKey)
                || headerBytes.Length != keyBytes.Length
                || !CryptographicOperations.FixedTimeEquals(headerBytes, keyBytes))
                return Results.Unauthorized();

            var assinaturas = await assinaturaRepository
                .ListarParaRenovarAsync(timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);

            var falhas = 0;
            foreach (var assinatura in assinaturas)
            {
                var result = await gerarHandler.HandleAsync(
                    new GerarCobrancaMensalCommand(assinatura.Id, assinatura.TreinadorId), cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    falhas++;
                    logger.LogWarning("Falha ao renovar assinatura {AssinaturaAlunoId}: {Erro}.",
                        assinatura.Id, result.Error?.Message);
                }
            }

            return Results.Ok(new { processadas = assinaturas.Count, falhas });
        })
        .WithTags("Internal")
        .WithSummary("Processa renovações mensais de assinaturas (requer X-Internal-Key)")
        .AllowAnonymous()
        .RequireRateLimiting("internal")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/internal/reconciliar-pagamentos", async (
            HttpContext httpContext,
            [FromBody] ReconciliarPagamentosStripeRequest? body,
            [FromServices] ReconciliarPagamentosStripeHandler handler,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var apiKey = configuration["Internal:ApiKey"];
            var headerKey = httpContext.Request.Headers["X-Internal-Key"].FirstOrDefault() ?? string.Empty;

            // Mesma defesa do endpoint de renovações: constant-time compare + checagem de tamanho.
            var headerBytes = Encoding.UTF8.GetBytes(headerKey);
            var keyBytes = Encoding.UTF8.GetBytes(apiKey ?? string.Empty);
            if (string.IsNullOrEmpty(apiKey)
                || headerBytes.Length != keyBytes.Length
                || !CryptographicOperations.FixedTimeEquals(headerBytes, keyBytes))
                return Results.Unauthorized();

            var command = new ReconciliarPagamentosStripeCommand(body?.DesdeUtc);
            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithTags("Internal")
        .WithSummary("Reconcilia eventos Stripe da janela especificada (default últimos 7d) — requer X-Internal-Key")
        .AllowAnonymous()
        .RequireRateLimiting("internal")
        .Produces<ReconciliarPagamentosStripeResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    /// <summary>
    /// Body opcional para <c>/internal/reconciliar-pagamentos</c>. Quando <c>null</c> ou ausente,
    /// o handler usa janela default de 7 dias a partir do <see cref="TimeProvider"/>.
    /// </summary>
    public sealed record ReconciliarPagamentosStripeRequest(DateTime? DesdeUtc);
}
