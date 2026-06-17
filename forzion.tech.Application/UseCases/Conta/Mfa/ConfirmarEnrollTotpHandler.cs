using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

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
    IValidator<ConfirmarEnrollTotpCommand> validator)
{
    private const int QuantidadeRecoveryCodes = 10;

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

        var raws = new List<string>(QuantidadeRecoveryCodes);
        var entidades = new List<MfaRecoveryCode>(QuantidadeRecoveryCodes);
        for (var i = 0; i < QuantidadeRecoveryCodes; i++)
        {
            var raw = GerarRecoveryCode();
            raws.Add(raw);
            entidades.Add(MfaRecoveryCode.Criar(mfa.ContaId, Hash(raw), agora).Value);
        }

        await recoveryCodeRepository.AdicionarRangeAsync(entidades, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new ConfirmarEnrollTotpResult(raws));
    }

    private static string GerarRecoveryCode() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
