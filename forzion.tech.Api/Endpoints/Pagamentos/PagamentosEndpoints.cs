using forzion.tech.Api.Extensions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
using forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;
using forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Endpoints.Pagamentos;

public static class PagamentosEndpoints
{
    private const int TamanhoLoteRenovacao = 200;

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
            if (!Enum.IsDefined(metodo))
                return Results.Problem(detail: "Método de pagamento inválido.", statusCode: StatusCodes.Status400BadRequest);

            var result = await handler.HandleAsync(
                new GerarCobrancaMensalCommand(assinaturaId, userContext.PerfilId, metodo), cancellationToken).ConfigureAwait(false);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Gera cobrança mensal para uma assinatura (metodo: Pix ou Cartao)")
        .Produces<PagamentoResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        treinadorGroup.MapGet("/recebimentos", async (
            [FromQuery] string? cursor,
            [FromQuery] int tamanho,
            [FromServices] ListarRecebimentosTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var resultado = await handler.HandleAsync(
                new ListarRecebimentosTreinadorQuery(userContext.PerfilId, cursor, tamanho), cancellationToken).ConfigureAwait(false);
            return Results.Ok(resultado);
        })
        .RequireRateLimiting("read")
        .WithSummary("Lista recebimentos do treinador (paginação keyset por cursor)")
        .Produces<ListarRecebimentosTreinadorResultado>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        var treinadorPlanoGroup = endpoints.MapGroup("/treinador/plano")
            .WithTags("Pagamentos")
            .RequireAuthorization("Treinador")
            .RequireRateLimiting("write");

        treinadorPlanoGroup.MapGet("/assinatura", async (
            [FromServices] IAssinaturaTreinadorRepository assinaturaTreinadorRepository,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var assinatura = await assinaturaTreinadorRepository
                .ObterAtualPorTreinadorAsync(userContext.PerfilId, cancellationToken).ConfigureAwait(false);
            if (assinatura is null) return Results.NotFound();
            return Results.Ok(new
            {
                assinaturaId = assinatura.Id,
                status = assinatura.Status.ToString(),
                valor = assinatura.Valor,
                planoPlataformaId = assinatura.PlanoPlataformaId,
                dataProximaCobranca = assinatura.DataProximaCobranca,
                planoPlataformaIdAgendado = assinatura.PlanoPlataformaIdAgendado
            });
        })
        .WithSummary("Retorna a assinatura de plano ativa do treinador")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        treinadorPlanoGroup.MapGet("/pagamento/{pagamentoId:guid}", async (
            Guid pagamentoId,
            [FromServices] IPagamentoTreinadorRepository pagamentoTreinadorRepository,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var pagamento = await pagamentoTreinadorRepository
                .ObterPorIdAsync(pagamentoId, cancellationToken).ConfigureAwait(false);
            if (pagamento is null || pagamento.TreinadorId != userContext.PerfilId)
                return Results.NotFound();
            return Results.Ok(new
            {
                pagamentoId = pagamento.Id,
                status = pagamento.Status.ToString(),
                valor = pagamento.Valor,
                metodo = pagamento.MetodoPagamento.ToString()
            });
        })
        .WithSummary("Retorna status de pagamento de plano do treinador")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        treinadorPlanoGroup.MapPost("/trocar", async (
            [FromBody] TrocarPlanoRequest body,
            [FromServices] TrocarPlanoTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new TrocarPlanoTreinadorCommand(userContext.PerfilId, body.PlanoPlataformaId, body.Metodo), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Troca o plano do treinador (upgrade/downgrade/regularização)")
        .Produces<TrocarPlanoTreinadorResponse>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status404NotFound);

        treinadorPlanoGroup.MapPost("/cobrar", async (
            [FromQuery] MetodoPagamento metodo,
            [FromServices] GerarCobrancaPlanoTreinadorHandler handler,
            [FromServices] IAssinaturaTreinadorRepository assinaturaTreinadorRepository,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.IsDefined(metodo))
                return Results.Problem(detail: "Método de pagamento inválido.", statusCode: StatusCodes.Status400BadRequest);

            var assinatura = await assinaturaTreinadorRepository
                .ObterAtualPorTreinadorAsync(userContext.PerfilId, cancellationToken).ConfigureAwait(false);
            if (assinatura is null)
                return Results.NotFound();
            var result = await handler.HandleAsync(
                new GerarCobrancaPlanoTreinadorCommand(assinatura.Id, metodo), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
                return result.ToProblemResult();
            if (result.Value.AssinaturaEncerrada)
                return Results.Ok(new { mensagem = "Downgrade para plano Free: assinatura encerrada sem cobrança." });
            return Results.Ok(result.Value);
        })
        .WithSummary("Gera cobrança de renovação do plano do treinador (metodo: Pix ou Cartao)")
        .Produces<IniciarPagamentoPlanoResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/processar-renovacoes-treinador", async (
            HttpContext httpContext,
            [FromServices] GerarCobrancaPlanoTreinadorHandler gerarHandler,
            [FromServices] IAssinaturaTreinadorRepository assinaturaTreinadorRepository,
            [FromServices] ILogger<Program> logger,
            [FromServices] TimeProvider timeProvider,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!ChaveInternaValida(httpContext, configuration))
                return Results.Unauthorized();

            var agora = timeProvider.GetUtcNow().UtcDateTime;
            var processadas = 0;
            var falhas = 0;
            Guid? aposId = null;
            while (true)
            {
                var lote = await assinaturaTreinadorRepository
                    .ListarParaRenovarAsync(agora, aposId, TamanhoLoteRenovacao, cancellationToken).ConfigureAwait(false);
                if (lote.Count == 0) break;

                foreach (var assinaturaId in lote.Select(a => a.Id))
                {
                    processadas++;
                    var result = await gerarHandler.HandleAsync(
                        new GerarCobrancaPlanoTreinadorCommand(assinaturaId), cancellationToken).ConfigureAwait(false);
                    if (result.IsFailure)
                    {
                        falhas++;
                        logger.LogWarning("Falha ao renovar assinatura de treinador {AssinaturaTreinadorId}: {Erro}.",
                            assinaturaId, result.Error?.Message);
                    }
                    else if (result.Value.AssinaturaEncerrada)
                    {
                        logger.LogInformation("Assinatura de treinador {AssinaturaTreinadorId} encerrada por downgrade para Free.",
                            assinaturaId);
                    }
                }

                aposId = lote[^1].Id;
                if (lote.Count < TamanhoLoteRenovacao) break;
            }

            return Results.Ok(new { processadas, falhas });
        })
        .WithTags("Internal")
        .WithSummary("Processa renovações mensais de planos de treinadores (requer X-Internal-Key)")
        .AllowAnonymous()
        .RequireRateLimiting("internal")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/internal/processar-renovacoes", async (
            HttpContext httpContext,
            [FromServices] GerarCobrancaMensalHandler gerarHandler,
            [FromServices] IAssinaturaAlunoRepository assinaturaRepository,
            [FromServices] ILogger<Program> logger,
            [FromServices] TimeProvider timeProvider,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!ChaveInternaValida(httpContext, configuration))
                return Results.Unauthorized();

            var agora = timeProvider.GetUtcNow().UtcDateTime;
            var processadas = 0;
            var falhas = 0;
            Guid? aposId = null;
            while (true)
            {
                var lote = await assinaturaRepository
                    .ListarParaRenovarAsync(agora, aposId, TamanhoLoteRenovacao, cancellationToken).ConfigureAwait(false);
                if (lote.Count == 0) break;

                foreach (var assinatura in lote)
                {
                    processadas++;
                    var result = await gerarHandler.HandleAsync(
                        new GerarCobrancaMensalCommand(assinatura.Id, assinatura.TreinadorId), cancellationToken).ConfigureAwait(false);

                    if (result.IsFailure)
                    {
                        falhas++;
                        logger.LogWarning("Falha ao renovar assinatura {AssinaturaAlunoId}: {Erro}.",
                            assinatura.Id, result.Error?.Message);
                    }
                }

                aposId = lote[^1].Id;
                if (lote.Count < TamanhoLoteRenovacao) break;
            }

            return Results.Ok(new { processadas, falhas });
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
            if (!ChaveInternaValida(httpContext, configuration))
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

    public sealed record TrocarPlanoRequest(Guid PlanoPlataformaId, MetodoPagamento Metodo = MetodoPagamento.Pix);

    private static bool ChaveInternaValida(HttpContext ctx, IConfiguration cfg) =>
        InternalApiKeyValidator.ChaveInternaValida(ctx, cfg);
}
