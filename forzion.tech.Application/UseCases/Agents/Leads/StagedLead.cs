namespace forzion.tech.Application.UseCases.Agents.Leads;

public sealed record StagedLead(string LeadId, string Source, string Status);
