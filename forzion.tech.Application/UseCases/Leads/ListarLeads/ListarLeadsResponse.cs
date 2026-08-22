using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Leads.ListarLeads;

public record ListarLeadsResponse(
    IReadOnlyList<LeadListItem> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);
