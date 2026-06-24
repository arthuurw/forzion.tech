using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.Mfa;

public record RegenerarRecoveryCodesResult(IReadOnlyList<string> RecoveryCodes);

public class RegenerarRecoveryCodesHandler(
    IUserContext userContext,
    IContaMfaRepository contaMfaRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogAprovacaoRepository logRepository,
    ILogger<RegenerarRecoveryCodesHandler> logger)
{
    public virtual async Task<Result<RegenerarRecoveryCodesResult>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (mfa is not { Habilitado: true })
            return Result.Failure<RegenerarRecoveryCodesResult>(MfaErrors.NaoHabilitado);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        await recoveryCodeRepository.RemoverPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);

        var codes = RecoveryCodeGenerator.Gerar();
        var raws = new List<string>(codes.Count);
        var entidades = new List<MfaRecoveryCode>(codes.Count);
        foreach (var (raw, hash) in codes)
        {
            raws.Add(raw);
            entidades.Add(MfaRecoveryCode.Criar(userContext.ContaId, hash, agora).Value);
        }

        await recoveryCodeRepository.AdicionarRangeAsync(entidades, cancellationToken).ConfigureAwait(false);

        var logResult = LogAprovacao.Registrar(TipoAcaoAprovacao.RecoveryCodesRegenerados, userContext.ContaId, userContext.ContaId, "Conta", agora);
        if (logResult.IsFailure)
            return Result.Failure<RegenerarRecoveryCodesResult>(logResult.Error!);
        await logRepository.AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Recovery codes regenerados — conta {ContaId}.", userContext.ContaId);
        return Result.Success(new RegenerarRecoveryCodesResult(raws));
    }
}
