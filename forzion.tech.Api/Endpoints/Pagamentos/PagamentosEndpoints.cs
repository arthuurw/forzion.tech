using forzion.tech.Api.Extensions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinatura;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
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
            return Results.Ok(result);
        })
        .WithSummary("Obtém status e QR code de um pagamento")
        .Produces<PagamentoResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        alunoGroup.MapGet("/assinatura/{assinaturaId:guid}", async (
            Guid assinaturaId,
            [FromServices] ListarPagamentosAssinaturaHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListarPagamentosAssinaturaQuery(assinaturaId, userContext.PerfilId), cancellationToken).ConfigureAwait(false);
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
            [FromServices] IAssinaturaRepository assinaturaRepository,
            [FromServices] ILogger<Program> logger,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var apiKey = configuration["Internal:ApiKey"];
            var headerKey = httpContext.Request.Headers["X-Internal-Key"].FirstOrDefault() ?? string.Empty;

            // Constant-time comparison para evitar timing attack na chave
            if (string.IsNullOrEmpty(apiKey) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(headerKey.PadRight(apiKey.Length)),
                Encoding.UTF8.GetBytes(apiKey)))
                return Results.Unauthorized();

            var assinaturas = await assinaturaRepository
                .ListarParaRenovarAsync(DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            var falhas = 0;
            foreach (var assinatura in assinaturas)
            {
                var result = await gerarHandler.HandleAsync(
                    new GerarCobrancaMensalCommand(assinatura.Id, assinatura.TreinadorId), cancellationToken).ConfigureAwait(false);

                if (result.IsFailure)
                {
                    falhas++;
                    logger.LogWarning("Falha ao renovar assinatura {AssinaturaId}: {Erro}.",
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

        return endpoints;
    }
}
