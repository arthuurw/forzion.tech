using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Auth.RenovarSessao;

public record RenovarSessaoCommand(string RefreshToken);

// Espelha LoginResponse + carrega o refresh rotacionado p/ o proxy reescrever o cookie.
public record RenovarSessaoResponse(string Token, string RefreshToken, TipoConta TipoConta, Guid ContaId, Guid PerfilId, string Nome);
