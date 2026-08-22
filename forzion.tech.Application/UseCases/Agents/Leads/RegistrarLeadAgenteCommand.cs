namespace forzion.tech.Application.UseCases.Agents.Leads;

public sealed record RegistrarLeadAgenteCommand(
    Guid TenantId,
    string Name,
    string ContactType,
    string ContactValue,
    string? Interest,
    bool ConsentGranted,
    string ConsentPurpose,
    DateTime? ConsentGrantedAt,
    string? IdempotencyKey,
    string? OriginUserAgent,
    string? OriginAssistant);
