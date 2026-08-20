using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Agents;

internal static class AgentProblem
{
    private const string TipoBase = "https://forzion.tech/problems/";

    // Título em inglês e estável: o consumidor é o gateway de agentes, que casa contra um
    // contrato versionado. Fica fora da customização PT-BR global de ProblemDetails de propósito.
    private static readonly IReadOnlyDictionary<AgentErrorCode, string> Titulos = new Dictionary<AgentErrorCode, string>
    {
        [AgentErrorCode.SignatureInvalid] = "Signature invalid",
        [AgentErrorCode.TimestampOutOfWindow] = "Timestamp out of window",
        [AgentErrorCode.ValidationFailed] = "Validation failed",
        [AgentErrorCode.TenantNotFound] = "Tenant not found",
        [AgentErrorCode.ServiceNotFound] = "Service not found",
        [AgentErrorCode.SlotNotFound] = "Slot not found",
        [AgentErrorCode.SlotUnavailable] = "Slot unavailable",
        [AgentErrorCode.IdempotencyConflict] = "Idempotency conflict",
        [AgentErrorCode.DependencyUnavailable] = "Dependency unavailable",
    };

    // Literal fixo por código: o motivo real da rejeição vai só para log server-side.
    private static readonly IReadOnlyDictionary<AgentErrorCode, string> Detalhes = new Dictionary<AgentErrorCode, string>
    {
        [AgentErrorCode.SignatureInvalid] = "The request signature could not be verified.",
        [AgentErrorCode.TimestampOutOfWindow] = "The request timestamp is outside the accepted window.",
        [AgentErrorCode.ValidationFailed] = "The request could not be validated.",
        [AgentErrorCode.TenantNotFound] = "The requested tenant does not exist.",
        [AgentErrorCode.ServiceNotFound] = "The requested service does not exist.",
        [AgentErrorCode.SlotNotFound] = "The requested slot does not exist.",
        [AgentErrorCode.SlotUnavailable] = "The requested slot is not available.",
        [AgentErrorCode.IdempotencyConflict] = "The idempotency key was already used with different content.",
        [AgentErrorCode.DependencyUnavailable] = "A required dependency is unavailable.",
    };

    internal static IResult Criar(AgentErrorCode code, int status)
    {
        var wire = code.ValorDeWire();
        var problema = new ProblemDetails
        {
            Type = TipoBase + wire,
            Title = Titulos[code],
            Status = status,
            Detail = Detalhes[code],
        };
        problema.Extensions["code"] = wire;
        return Results.Problem(problema);
    }
}
