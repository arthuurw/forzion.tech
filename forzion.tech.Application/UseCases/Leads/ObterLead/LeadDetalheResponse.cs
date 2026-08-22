using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Leads.ObterLead;

public record LeadInteracaoResponse(
    Guid Id,
    DateTime OcorridaEm,
    LeadStatus? StatusAnterior,
    LeadStatus? StatusNovo,
    string? Observacao,
    Guid? RealizadoPorId);

public record LeadDetalheResponse(
    Guid Id,
    string Nome,
    TipoContatoLead ContatoTipo,
    string ContatoValor,
    string? Interesse,
    string ConsentimentoFinalidade,
    DateTime ConsentimentoConcedidoEm,
    DateTime ConsentimentoRegistradoEm,
    string? OrigemUserAgent,
    string? OrigemAssistente,
    LeadSource Source,
    LeadStatus Status,
    MotivoDescarteLead? MotivoDescarte,
    Guid? AlunoId,
    bool Anonimizado,
    DateTime UltimoToqueEm,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<LeadInteracaoResponse> Historico);
