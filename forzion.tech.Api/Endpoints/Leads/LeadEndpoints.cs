using forzion.tech.Api.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Leads.AtualizarStatusLead;
using forzion.tech.Application.UseCases.Leads.CriarLeadManual;
using forzion.tech.Application.UseCases.Leads.EnviarConvite;
using forzion.tech.Application.UseCases.Leads.ListarLeads;
using forzion.tech.Application.UseCases.Leads.ObterLead;
using forzion.tech.Application.UseCases.Leads.ObterMetricas;
using forzion.tech.Application.UseCases.Leads.RegistrarInteracao;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Leads;

public static class LeadEndpoints
{
    public static IEndpointRouteBuilder MapLeadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/treinador/leads").WithTags("Leads").RequireAuthorization("Treinador")
            .RequireRateLimiting("write")
            .AddEndpointFilter<PaginacaoFilter>();

        group.MapGet("/", async (
            [FromServices] ListarLeadsHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var q = httpContext.Request.Query;
            _ = int.TryParse(q["pagina"], out var pagina);
            _ = int.TryParse(q["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : Math.Clamp(tamanhoPagina, 1, 100);

            var status = Enum.TryParse<LeadStatus>(q["status"], ignoreCase: true, out var statusParsed) ? statusParsed : (LeadStatus?)null;
            var origem = Enum.TryParse<LeadSource>(q["origem"], ignoreCase: true, out var origemParsed) ? origemParsed : (LeadSource?)null;
            var inicio = DateTime.TryParse(q["inicio"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var inicioParsed) ? inicioParsed : (DateTime?)null;
            var fim = DateTime.TryParse(q["fim"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var fimParsed) ? fimParsed : (DateTime?)null;
            var termo = q["termo"].FirstOrDefault();

            var query = new ListarLeadsQuery(userContext.PerfilId, p, tp, status, origem, inicio, fim, termo);
            var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireRateLimiting("read")
        .WithSummary("Lista leads do treinador com filtros e paginação")
        .Produces<ListarLeadsResponse>();

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ObterLeadHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ObterLeadQuery(userContext.PerfilId, id), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .RequireRateLimiting("read")
        .WithSummary("Obtém o detalhe de um lead do treinador, com histórico")
        .Produces<LeadDetalheResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            [FromBody] CriarLeadManualRequest request,
            [FromServices] CriarLeadManualHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarLeadManualCommand(userContext.PerfilId, request.Nome, request.ContatoTipo, request.ContatoValor, request.Interesse, request.ConsentimentoFinalidade),
                cancellationToken).ConfigureAwait(false);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/treinador/leads/{result.Value.LeadId}", result.Value);
        })
        .WithSummary("Cadastra um lead manualmente")
        .Produces<CriarLeadManualResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] AtualizarStatusLeadRequest request,
            [FromServices] AtualizarStatusLeadHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarStatusLeadCommand(userContext.PerfilId, id, userContext.PerfilId, request.NovoStatus, request.Motivo, request.Observacao),
                cancellationToken).ConfigureAwait(false);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Transiciona o status de um lead (em contato ou descartado)")
        .Produces<AtualizarStatusLeadResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/interacoes", async (
            Guid id,
            [FromBody] RegistrarInteracaoLeadRequest request,
            [FromServices] RegistrarInteracaoLeadHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RegistrarInteracaoLeadCommand(userContext.PerfilId, id, userContext.PerfilId, request.Observacao),
                cancellationToken).ConfigureAwait(false);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Registra uma interação livre no histórico do lead")
        .Produces<RegistrarInteracaoLeadResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/convite", async (
            Guid id,
            [FromServices] EnviarConviteLeadHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new EnviarConviteLeadCommand(userContext.PerfilId, id), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Emite convite de cadastro para o lead (invalida convite ativo anterior)")
        .Produces<EnviarConviteLeadResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/metricas", async (
            [FromServices] ObterMetricasLeadsHandler handler,
            [FromServices] IUserContext userContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hoje = DateTime.UtcNow.Date;
            var q = httpContext.Request.Query;

            var inicio = DateTime.TryParse(q["inicio"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var inicioParsed)
                ? inicioParsed.Date
                : hoje.AddDays(-30);
            var fim = DateTime.TryParse(q["fim"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var fimParsed)
                ? fimParsed.Date
                : hoje;

            if (inicio > fim)
                return Results.Problem(detail: "O parâmetro 'inicio' deve ser anterior a 'fim'.", statusCode: 400);

            // fim é sempre uma data pura (meia-noite) — sem estender até o fim do dia, um lead
            // criado mais tarde hoje (ou em qualquer instante > 00:00 de um 'fim' explícito) cai
            // fora de "CreatedAt <= fim" e some da métrica.
            var fimInclusivo = fim.AddDays(1).AddTicks(-1);

            var result = await handler.HandleAsync(new ObterMetricasLeadsQuery(userContext.PerfilId, inicio, fimInclusivo), cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireRateLimiting("read")
        .WithSummary("Métricas agregadas do canal de leads do treinador no período")
        .Produces<ObterMetricasLeadsResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}

public record CriarLeadManualRequest(
    string Nome,
    TipoContatoLead ContatoTipo,
    string ContatoValor,
    string? Interesse,
    string ConsentimentoFinalidade);

public record AtualizarStatusLeadRequest(LeadStatus NovoStatus, MotivoDescarteLead? Motivo, string? Observacao);

public record RegistrarInteracaoLeadRequest(string Observacao);
