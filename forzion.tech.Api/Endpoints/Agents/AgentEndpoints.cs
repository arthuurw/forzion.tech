using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Application.UseCases.Agents.Agendamentos;
using forzion.tech.Application.UseCases.Agents.BusinessInfo;
using forzion.tech.Application.UseCases.Agents.Disponibilidade;
using forzion.tech.Application.UseCases.Agents.Leads;
using forzion.tech.Application.UseCases.Agents.Services;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Globalization;
using System.Text.Json;

namespace forzion.tech.Api.Endpoints.Agents;

public static class AgentEndpoints
{
    internal const string Prefixo = "/internal/agents/v1";
    internal const string TagAgentsReady = "agents-ready";

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var grupo = CriarGrupo(endpoints);

        grupo.MapGet("/health", async (
            [FromServices] HealthCheckService saude,
            [FromServices] TimeProvider relogio,
            CancellationToken cancellationToken) =>
        {
            var relatorio = await saude
                .CheckHealthAsync(registro => registro.Tags.Contains(TagAgentsReady), cancellationToken)
                .ConfigureAwait(false);

            // Só o status agregado e o instante saem daqui: nome de check, descrição e exceção
            // descrevem a topologia interna e não vão para o chamador.
            return relatorio.Status switch
            {
                HealthStatus.Unhealthy => AgentProblem.Criar(
                    AgentErrorCode.DependencyUnavailable, StatusCodes.Status503ServiceUnavailable),
                HealthStatus.Degraded => Results.Ok(new AgentHealth("degraded", relogio.GetUtcNow())),
                _ => Results.Ok(new AgentHealth("healthy", relogio.GetUtcNow())),
            };
        });

        grupo.MapGet("/tenants/{tenantId}/business-info", async Task<IResult> (
            string tenantId,
            [FromServices] ObterBusinessInfoHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            var result = await handler.HandleAsync(new ObterBusinessInfoQuery(tenantGuid), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : AgentProblem.Criar(AgentErrorCode.TenantNotFound, StatusCodes.Status404NotFound);
        });

        grupo.MapGet("/tenants/{tenantId}/services", async Task<IResult> (
            string tenantId,
            string? category,
            [FromServices] ListarServicosHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);
            if (category is { Length: > 100 })
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            var result = await handler.HandleAsync(new ListarServicosQuery(tenantGuid, category), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : AgentProblem.Criar(AgentErrorCode.TenantNotFound, StatusCodes.Status404NotFound);
        });

        grupo.MapPost("/tenants/{tenantId}/leads", async Task<IResult> (
            string tenantId,
            HttpContext httpContext,
            [FromServices] RegistrarLeadAgenteHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            // [FromBody] em tipo complexo faria o binder ler o corpo ANTES da cadeia de filtros —
            // o HmacSignatureFilter, mais interno, hashearia um corpo já drenado (vazio) e toda
            // requisição assinada corretamente cairia em signature_invalid. Leitura manual aqui
            // roda dentro do handler, depois do filtro já ter recolocado o corpo bufferizado.
            StageLeadRequest? request;
            try
            {
                request = await httpContext.Request.ReadFromJsonAsync<StageLeadRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);
            }

            if (request?.Contact is null || request.Consent is null)
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            var command = new RegistrarLeadAgenteCommand(
                tenantGuid,
                request.Name,
                request.Contact.Type,
                request.Contact.Value,
                request.Interest,
                request.Consent.Granted,
                request.Consent.Purpose,
                request.Consent.GrantedAt,
                request.IdempotencyKey,
                request.Origin?.UserAgent,
                request.Origin?.Assistant);

            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
                return Results.Json(result.Value, statusCode: StatusCodes.Status201Created);

            return result.Error!.Type switch
            {
                ErrorType.NotFound => AgentProblem.Criar(AgentErrorCode.TenantNotFound, StatusCodes.Status404NotFound),
                ErrorType.Conflict => AgentProblem.Criar(AgentErrorCode.IdempotencyConflict, StatusCodes.Status409Conflict),
                _ => AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest),
            };
        });

        grupo.MapGet("/tenants/{tenantId}/availability", async Task<IResult> (
            string tenantId,
            string? serviceId,
            string? from,
            string? to,
            [FromServices] ConsultarDisponibilidadeAgenteHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid) || !Guid.TryParse(serviceId, out var serviceGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            if (!DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromOffset)
                || !DateTimeOffset.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toOffset))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            if (toOffset <= fromOffset || toOffset - fromOffset > TimeSpan.FromDays(31))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            var query = new ConsultarDisponibilidadeQuery(tenantGuid, serviceGuid, fromOffset.UtcDateTime, toOffset.UtcDateTime);
            var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
                return Results.Ok(result.Value);

            // Switch por instância de erro, não por ErrorType: os dois 404 (tenant/serviço) têm o
            // mesmo ErrorType.NotFound e precisam permanecer distintos (risco R8 do design).
            return result.Error switch
            {
                { } e when e == TreinadorErrors.NaoEncontrado => AgentProblem.Criar(AgentErrorCode.TenantNotFound, StatusCodes.Status404NotFound),
                { } e when e == PacoteErrors.NaoEncontrado => AgentProblem.Criar(AgentErrorCode.ServiceNotFound, StatusCodes.Status404NotFound),
                _ => AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest),
            };
        });

        grupo.MapPost("/tenants/{tenantId}/booking-requests", async Task<IResult> (
            string tenantId,
            HttpContext httpContext,
            [FromServices] RegistrarSolicitacaoAgendamentoHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            // Mesmo motivo do POST leads: leitura manual do corpo aqui, depois que o
            // HmacSignatureFilter (mais interno) já recolocou o corpo bufferizado.
            StageBookingRequestRequest? request;
            try
            {
                request = await httpContext.Request.ReadFromJsonAsync<StageBookingRequestRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);
            }

            if (request?.Contact is null || request.Consent is null || !Guid.TryParse(request.ServiceId, out var serviceGuid))
                return AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);

            var command = new RegistrarSolicitacaoAgendamentoCommand(
                tenantGuid,
                serviceGuid,
                request.SlotId,
                request.Name,
                request.Contact.Type,
                request.Contact.Value,
                request.Consent.Granted,
                request.Consent.Purpose,
                request.Consent.GrantedAt,
                request.IdempotencyKey,
                request.Origin?.UserAgent,
                request.Origin?.Assistant);

            var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
                return Results.Json(result.Value, statusCode: StatusCodes.Status201Created);

            // Switch por instância de erro, não por ErrorType: slot_unavailable e idempotency_conflict
            // são ambos ErrorType.Conflict, e tenant/service/slot not_found são todos ErrorType.NotFound
            // — precisam permanecer distintos no wire (mesmo precedente de GET availability).
            return result.Error switch
            {
                { } e when e == TreinadorErrors.NaoEncontrado => AgentProblem.Criar(AgentErrorCode.TenantNotFound, StatusCodes.Status404NotFound),
                { } e when e == PacoteErrors.NaoEncontrado => AgentProblem.Criar(AgentErrorCode.ServiceNotFound, StatusCodes.Status404NotFound),
                { } e when e == SolicitacaoAgendamentoAgenteErrors.SlotNaoEncontrado => AgentProblem.Criar(AgentErrorCode.SlotNotFound, StatusCodes.Status404NotFound),
                { } e when e == SolicitacaoAgendamentoAgenteErrors.SlotIndisponivel => AgentProblem.Criar(AgentErrorCode.SlotUnavailable, StatusCodes.Status409Conflict),
                { } e when e == SolicitacaoAgendamentoAgenteErrors.IdempotencyConflito => AgentProblem.Criar(AgentErrorCode.IdempotencyConflict, StatusCodes.Status409Conflict),
                _ => AgentProblem.Criar(AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest),
            };
        });

        return endpoints;
    }

    // Assinatura, rate limit e exclusão do OpenAPI ficam no GRUPO: endpoint acrescentado aqui
    // nasce protegido sem declarar nada, e esquecer a anotação deixa de ser uma forma de abrir
    // a superfície. Exposto para o teste montar um endpoint no mesmo grupo que a produção usa.
    internal static RouteGroupBuilder CriarGrupo(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup(Prefixo)
            .AddEndpointFilter<AgentExceptionFilter>()
            .AddEndpointFilter<HmacSignatureFilter>()
            .RequireRateLimiting("agents")
            .AllowAnonymous()
            .ExcludeFromDescription();

    internal sealed record AgentHealth(string Status, DateTimeOffset CheckedAt);

    internal sealed record StageLeadContactRequest(string Type, string Value);

    internal sealed record StageLeadConsentRequest(bool Granted, string Purpose, DateTime? GrantedAt);

    internal sealed record StageLeadOriginRequest(string? UserAgent, string? Assistant);

    internal sealed record StageLeadRequest(
        string Name,
        StageLeadContactRequest Contact,
        string? Interest,
        StageLeadConsentRequest Consent,
        string? IdempotencyKey,
        StageLeadOriginRequest? Origin);

    internal sealed record StageBookingRequestContactRequest(string Type, string Value);

    internal sealed record StageBookingRequestConsentRequest(bool Granted, string Purpose, DateTime? GrantedAt);

    internal sealed record StageBookingRequestOriginRequest(string? UserAgent, string? Assistant);

    internal sealed record StageBookingRequestRequest(
        string ServiceId,
        string SlotId,
        string Name,
        StageBookingRequestContactRequest Contact,
        StageBookingRequestConsentRequest Consent,
        string IdempotencyKey,
        StageBookingRequestOriginRequest? Origin);
}
