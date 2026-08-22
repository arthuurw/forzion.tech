using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Leads.ListarLeads;

public record ListarLeadsQuery(
    Guid TreinadorId,
    int Pagina = 1,
    int TamanhoPagina = 20,
    LeadStatus? Status = null,
    LeadSource? Origem = null,
    DateTime? Inicio = null,
    DateTime? Fim = null,
    string? Termo = null);
