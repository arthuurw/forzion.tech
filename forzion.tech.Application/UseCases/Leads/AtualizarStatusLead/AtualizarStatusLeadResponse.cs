using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Leads.AtualizarStatusLead;

public record AtualizarStatusLeadResponse(Guid LeadId, LeadStatus Status);
