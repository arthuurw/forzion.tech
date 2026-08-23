namespace forzion.tech.Application.UseCases.Agents.Disponibilidade;

public sealed record ConsultarDisponibilidadeQuery(Guid TenantId, Guid ServiceId, DateTime FromUtc, DateTime ToUtc);
