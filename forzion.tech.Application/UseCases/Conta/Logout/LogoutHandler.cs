using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.Logout;

public class LogoutHandler(
    ITokenRevogadoRepository tokenRevogadoRepository,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<LogoutHandler> logger)
{
    public virtual async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        var jti = userContext.Jti;
        var expiraEm = userContext.TokenExpiraEm;
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (jti == Guid.Empty || expiraEm <= agora)
        {
            logger.LogWarning("Logout com token sem jti válido ou já expirado.");
            return;
        }

        try
        {
            var tokenResult = TokenRevogado.Criar(jti, expiraEm, agora);
            if (tokenResult.IsFailure)
                throw new DomainException(tokenResult.Error!.Message);

            await tokenRevogadoRepository
                .AdicionarAsync(tokenResult.Value, cancellationToken)
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
