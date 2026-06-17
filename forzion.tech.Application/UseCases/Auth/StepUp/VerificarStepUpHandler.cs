using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Auth;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Auth.StepUp;

public record VerificarStepUpCommand(string Codigo);

public record VerificarStepUpResult(string Token, DateTime ExpiraEm);

public class VerificarStepUpCommandValidator : AbstractValidator<VerificarStepUpCommand>
{
    public VerificarStepUpCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty();
    }
}

public class VerificarStepUpHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IContaMfaRepository contaMfaRepository,
    IMfaChallengeRepository challengeRepository,
    ITotpService totpService,
    IMfaSecretProtector secretProtector,
    IJwtService jwtService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<VerificarStepUpCommand> validator)
{
    private static readonly TimeSpan ValidadeStepUp = TimeSpan.FromMinutes(5);

    public virtual Task<Result<VerificarStepUpResult>> HandleAsync(
        VerificarStepUpCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<VerificarStepUpResult>> HandleAsyncCore(
        VerificarStepUpCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure<VerificarStepUpResult>(MfaErrors.ContaIdInvalido);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);

        var verificacao = mfa is { Habilitado: true }
            ? VerificarTotp(mfa, command.Codigo, agora)
            : await VerificarEmailAsync(conta.Id, command.Codigo, agora, cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (verificacao.IsFailure)
            return Result.Failure<VerificarStepUpResult>(verificacao.Error!);

        var token = jwtService.GerarTokenEscopo(conta, MfaScopes.StepUp, ValidadeStepUp);
        return Result.Success(new VerificarStepUpResult(token.Token, token.ExpiraEm));
    }

    private Result VerificarTotp(ContaMfa mfa, string codigo, DateTime agora)
    {
        var secret = secretProtector.Revelar(mfa.TotpSecretCifrado!);
        var verificacao = totpService.Verificar(secret, codigo, mfa.UltimoTimeStep);
        if (!verificacao.Valido)
            return Result.Failure(MfaErrors.CodigoInvalido);

        return mfa.RegistrarUso(verificacao.TimeStep, agora);
    }

    private async Task<Result> VerificarEmailAsync(Guid contaId, string codigo, DateTime agora, CancellationToken cancellationToken)
    {
        var challenge = await challengeRepository
            .BuscarUltimoPorContaEPropositoAsync(contaId, MfaProposito.StepUp, cancellationToken)
            .ConfigureAwait(false);
        if (challenge is null)
            return Result.Failure(MfaErrors.CodigoInvalido);

        var validacao = challenge.Validar(agora);
        if (validacao.IsFailure)
            return validacao;

        if (!CodigoConfere(codigo, challenge.CodigoHash))
        {
            challenge.RegistrarTentativa();
            return Result.Failure(MfaErrors.CodigoInvalido);
        }

        return challenge.MarcarUsado(agora);
    }

    private static bool CodigoConfere(string codigo, string hashEsperado) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(codigo)),
            Encoding.UTF8.GetBytes(hashEsperado));

    private static string Hash(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();
}
