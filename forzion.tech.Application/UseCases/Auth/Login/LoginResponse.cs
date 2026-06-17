using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Auth.Login;

public record LoginResponse(
    string Token,
    string RefreshToken,
    TipoConta TipoConta,
    Guid ContaId,
    Guid PerfilId,
    string Nome,
    bool MfaRequerido = false,
    string? MfaPendingToken = null,
    DateTime? MfaPendingExpiraEm = null)
{
    public static LoginResponse Pendente(string pendingToken, DateTime expiraEm) =>
        new(string.Empty, string.Empty, default, Guid.Empty, Guid.Empty, string.Empty, true, pendingToken, expiraEm);
}
