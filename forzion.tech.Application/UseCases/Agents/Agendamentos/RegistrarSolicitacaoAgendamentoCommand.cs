namespace forzion.tech.Application.UseCases.Agents.Agendamentos;

public sealed record RegistrarSolicitacaoAgendamentoCommand(
    Guid TenantId,
    Guid ServiceId,
    string SlotId,
    string Name,
    string ContactType,
    string ContactValue,
    bool ConsentGranted,
    string ConsentPurpose,
    DateTime? ConsentGrantedAt,
    string IdempotencyKey,
    string? OriginUserAgent,
    string? OriginAssistant);
