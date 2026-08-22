using forzion.tech.Domain.Enums;

namespace forzion.tech.Domain.Events;

public sealed record LeadCriadoEvent(
    Guid LeadId,
    Guid TreinadorId,
    LeadSource Source,
    DateTime OcorridoEm) : IDomainEvent;
