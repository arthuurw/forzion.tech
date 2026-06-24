using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.Mfa;

public record ConfirmarEnrollTotpCommand(string Codigo);

public record ConfirmarEnrollTotpResult(IReadOnlyList<string> RecoveryCodes);

public class ConfirmarEnrollTotpCommandValidator : AbstractValidator<ConfirmarEnrollTotpCommand>
{
    public ConfirmarEnrollTotpCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty();
    }
}

public class ConfirmarEnrollTotpHandler(
    IUserContext userContext,
    IContaMfaRepository contaMfaRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    ITotpService totpService,
    IMfaSecretProtector secretProtector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<ConfirmarEnrollTotpCommand> validator,
    ILogAprovacaoRepository logRepository,
    ILogger<ConfirmarEnrollTotpHandler> logger)
{
    public virtual Task<Result<ConfirmarEnrollTotpResult>> HandleAsync(
        ConfirmarEnrollTotpCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<ConfirmarEnrollTotpResult>> HandleAsyncCore(
        ConfirmarEnrollTotpCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (mfa is null || string.IsNullOrWhiteSpace(mfa.TotpSecretCifrado))
            return Result.Failure<ConfirmarEnrollTotpResult>(MfaErrors.EnrollNaoIniciado);

        if (mfa.Habilitado)
            return Result.Failure<ConfirmarEnrollTotpResult>(MfaErrors.JaConfirmado);

        var secret = secretProtector.Revelar(mfa.TotpSecretCifrado);
        var verificacao = totpService.Verificar(secret, command.Codigo, mfa.UltimoTimeStep);
        if (!verificacao.Valido)
            return Result.Failure<ConfirmarEnrollTotpResult>(MfaErrors.CodigoInvalido);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var confirmar = mfa.Confirmar(verificacao.TimeStep, agora);
        if (confirmar.IsFailure)
            return Result.Failure<ConfirmarEnrollTotpResult>(confirmar.Error!);

        var codes = RecoveryCodeGenerator.Gerar();
        var raws = new List<string>(codes.Count);
        var entidades = new List<MfaRecoveryCode>(codes.Count);
        foreach (var (raw, hash) in codes)
        {
            raws.Add(raw);
            entidades.Add(MfaRecoveryCode.Criar(mfa.ContaId, hash, agora).Value);
        }

        await recoveryCodeRepository.AdicionarRangeAsync(entidades, cancellationToken).ConfigureAwait(false);

        var logResult = await logRepository.RegistrarAsync(TipoAcaoAprovacao.MfaHabilitado, userContext.ContaId, userContext.ContaId, "Conta", agora, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (logResult.IsFailure)
            return Result.Failure<ConfirmarEnrollTotpResult>(logResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("MFA habilitado — conta {ContaId}.", userContext.ContaId);

        return Result.Success(new ConfirmarEnrollTotpResult(raws));
    }
}
