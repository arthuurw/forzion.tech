using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Leads.CriarLeadManual;

public record CriarLeadManualCommand(
    Guid TreinadorId,
    string Nome,
    TipoContatoLead ContatoTipo,
    string ContatoValor,
    string? Interesse,
    string ConsentimentoFinalidade);
