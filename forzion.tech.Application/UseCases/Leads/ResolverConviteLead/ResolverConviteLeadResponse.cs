using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Leads.ResolverConviteLead;

public record ResolverConviteLeadResponse(
    string Nome,
    TipoContatoLead ContatoTipo,
    string ContatoValor,
    Guid TreinadorId,
    string TreinadorNome);
