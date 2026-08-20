namespace forzion.tech.Api.Endpoints.Agents;

internal enum AgentErrorCode
{
    SignatureInvalid,
    TimestampOutOfWindow,
    ValidationFailed,
    TenantNotFound,
    ServiceNotFound,
    SlotNotFound,
    SlotUnavailable,
    IdempotencyConflict,
    DependencyUnavailable,
}

internal static class AgentErrorCodeExtensions
{
    private static readonly IReadOnlyDictionary<AgentErrorCode, string> Wire = new Dictionary<AgentErrorCode, string>
    {
        [AgentErrorCode.SignatureInvalid] = "signature_invalid",
        [AgentErrorCode.TimestampOutOfWindow] = "timestamp_out_of_window",
        [AgentErrorCode.ValidationFailed] = "validation_failed",
        [AgentErrorCode.TenantNotFound] = "tenant_not_found",
        [AgentErrorCode.ServiceNotFound] = "service_not_found",
        [AgentErrorCode.SlotNotFound] = "slot_not_found",
        [AgentErrorCode.SlotUnavailable] = "slot_unavailable",
        [AgentErrorCode.IdempotencyConflict] = "idempotency_conflict",
        [AgentErrorCode.DependencyUnavailable] = "dependency_unavailable",
    };

    internal static string ValorDeWire(this AgentErrorCode code) => Wire[code];
}
