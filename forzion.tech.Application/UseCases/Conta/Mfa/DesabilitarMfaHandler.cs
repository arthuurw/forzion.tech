using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.Mfa;

public class DesabilitarMfaHandler(
    IUserContext userContext,
    IContaMfaRepository contaMfaRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    ITrustedDeviceRepository trustedDeviceRepository,
    ITokenRevogadoRepository tokenRevogadoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogAprovacaoRepository logRepository,
    ILogger<DesabilitarMfaHandler> logger)
{
    public virtual async Task<Result> HandleAsync(CancellationToken cancellationToken = default)
    {
        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (mfa is not { Habilitado: true })
            return Result.Failure(MfaErrors.NaoHabilitado);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        mfa.Desabilitar(agora);
        await recoveryCodeRepository.RemoverPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        await trustedDeviceRepository.RemoverPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);

        var jti = userContext.Jti;
        var tokenExpiraEm = userContext.TokenExpiraEm;
        if (jti != Guid.Empty && tokenExpiraEm > agora)
        {
            var tokenResult = TokenRevogado.Criar(jti, tokenExpiraEm, agora);
            if (tokenResult.IsFailure)
                return Result.Failure(tokenResult.Error!);
            await tokenRevogadoRepository.AdicionarAsync(tokenResult.Value, cancellationToken).ConfigureAwait(false);
        }

        var logResult = LogAprovacao.Registrar(TipoAcaoAprovacao.MfaDesabilitado, userContext.ContaId, userContext.ContaId, "Conta", agora);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);
        await logRepository.AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("MFA desabilitado — conta {ContaId}.", userContext.ContaId);
        return Result.Success();
    }
}
