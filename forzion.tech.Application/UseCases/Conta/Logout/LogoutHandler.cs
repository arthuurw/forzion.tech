using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.Logout;

public class LogoutHandler(
    ITokenRevogadoRepository tokenRevogadoRepository,
    IRefreshTokenService refreshTokenService,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IDatabaseErrorInspector databaseErrorInspector,
    TimeProvider timeProvider,
    ILogger<LogoutHandler> logger)
{
    public virtual async Task<Result> HandleAsync(CancellationToken cancellationToken = default)
    {
        var jti = userContext.Jti;
        var expiraEm = userContext.TokenExpiraEm;
        var familiaId = userContext.FamiliaId;
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        // Revoga a família deste device: mata o refresh, não só o access via jti.
        if (familiaId != Guid.Empty)
            await refreshTokenService.RevogarFamiliaAsync(familiaId, MotivoRevogacaoFamilia.Logout, agora, cancellationToken).ConfigureAwait(false);

        if (jti == Guid.Empty || expiraEm <= agora)
        {
            if (familiaId != Guid.Empty)
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Logout com token sem jti válido ou já expirado.");
            return Result.Success();
        }

        try
        {
            var tokenResult = TokenRevogado.Criar(jti, expiraEm, agora);
            if (tokenResult.IsFailure)
                return Result.Failure(tokenResult.Error!);

            await tokenRevogadoRepository
                .AdicionarAsync(tokenResult.Value, cancellationToken)
                .ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Token revogado no logout. Jti={Jti}", jti);
        }
        catch (Exception ex) when (databaseErrorInspector.EhViolacaoDeUnicidade(ex))
        {
            logger.LogDebug(ex, "Token Jti={Jti} já estava revogado (logout simultâneo).", jti);
        }

        return Result.Success();
    }
}
