namespace forzion.tech.Application.UseCases.Agents.Disponibilidade;

public sealed record AvailabilitySlotResponse(
    string SlotId,
    string ServiceId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int CapacityRemaining);
