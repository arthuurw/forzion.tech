using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Auth.Login;

public record LoginResponse(string Token, TipoConta TipoConta, Guid ContaId, Guid PerfilId);
