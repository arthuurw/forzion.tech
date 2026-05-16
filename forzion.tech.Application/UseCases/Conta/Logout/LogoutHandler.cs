using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.Logout;

public class LogoutHandler(
    ITokenRevogadoRepository tokenRevogadoRepository,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    ILogger<LogoutHandler> logger)
{
    public virtual async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        var jti = userContext.Jti;
        var expiraEm = userContext.TokenExpiraEm;

        if (jti == Guid.Empty || expiraEm <= DateTime.UtcNow)
        {
            logger.LogWarning("Logout com token sem jti válido ou já expirado.");
            return;
        }

        try
        {
            await tokenRevogadoRepository
                .AdicionarAsync(TokenRevogado.Criar(jti, expiraEm), cancellationToken)
                .ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Token revogado no logout. Jti={Jti}", jti);
        }
        catch (Exception ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Token já revogado por logout simultâneo — idempotente
            logger.LogDebug(ex, "Token Jti={Jti} já estava revogado (logout simultâneo).", jti);
        }
    }
}
