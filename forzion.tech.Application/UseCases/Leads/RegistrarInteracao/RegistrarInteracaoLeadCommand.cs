namespace forzion.tech.Application.UseCases.Leads.RegistrarInteracao;

public record RegistrarInteracaoLeadCommand(
    Guid TreinadorId,
    Guid LeadId,
    Guid RealizadoPorId,
    string Observacao);
