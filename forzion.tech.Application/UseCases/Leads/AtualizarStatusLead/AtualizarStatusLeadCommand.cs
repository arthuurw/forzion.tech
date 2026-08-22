using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Leads.AtualizarStatusLead;

public record AtualizarStatusLeadCommand(
    Guid TreinadorId,
    Guid LeadId,
    Guid RealizadoPorId,
    LeadStatus NovoStatus,
    MotivoDescarteLead? Motivo,
    string? Observacao);
