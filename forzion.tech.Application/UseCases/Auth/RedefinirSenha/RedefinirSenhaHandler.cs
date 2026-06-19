using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Validation;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Auth.RedefinirSenha;

public record RedefinirSenhaCommand(string Token, string NovaSenha, string? CodigoTotp = null);

public class RedefinirSenhaCommandValidator : AbstractValidator<RedefinirSenhaCommand>
{
    public RedefinirSenhaCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("O token é obrigatório.")
            .Length(64).WithMessage("Token inválido.");

        RuleFor(x => x.NovaSenha).SenhaForte();
    }
}

public class RedefinirSenhaHandler(
    IPasswordResetTokenRepository tokenRepository,
    IContaRepository contaRepository,
    IContaMfaRepository contaMfaRepository,
    ITrustedDeviceRepository trustedDeviceRepository,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokenService,
    ITotpService totpService,
    IMfaSecretProtector secretProtector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<RedefinirSenhaCommand> validator)
{
    public virtual Task<Result> HandleAsync(
        RedefinirSenhaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        RedefinirSenhaCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var hash = ComputeHash(command.Token);

        var token = await tokenRepository.BuscarPorHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (token is null || token.UsedAt.HasValue)
            return Result.Failure(Error.Business("auth_reset.token_invalido", "Token inválido ou já utilizado."));

        var agoraOffset = timeProvider.GetUtcNow();
        var agora = agoraOffset.UtcDateTime;

        if (token.ExpiresAt < agora)
            return Result.Failure(Error.Business("auth_reset.token_expirado", "Token expirado. Solicite um novo link de redefinição."));

        var conta = await contaRepository.ObterPorIdAsync(token.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new EstadoInconsistenteException("Conta não encontrada.");

        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
        if (mfa is { Habilitado: true })
        {
            var totpResult = VerificarTotp(mfa, command.CodigoTotp, agora);
            if (totpResult.IsFailure)
                return Result.Failure(totpResult.Error!);
        }

        var atualizarResult = conta.AtualizarSenha(passwordHasher.Hash(command.NovaSenha), agora);
        if (atualizarResult.IsFailure)
            return Result.Failure(atualizarResult.Error!);

        conta.InvalidarSessoesAnteriores(agoraOffset);

        var marcarResult = token.MarcarComoUsado(agora);
        if (marcarResult.IsFailure)
            return Result.Failure(marcarResult.Error!);

        // Reset de senha revoga todas as sessões da conta (NR-6) — inclui o device de quem roubou a senha.
        await refreshTokenService.RevogarTodasPorContaAsync(conta.Id, MotivoRevogacaoFamilia.TrocaSenha, agora, cancellationToken).ConfigureAwait(false);
        await trustedDeviceRepository.RemoverPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private Result VerificarTotp(Domain.Entities.ContaMfa mfa, string? codigo, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Result.Failure(MfaErrors.CodigoInvalido);

        var secret = secretProtector.Revelar(mfa.TotpSecretCifrado!);
        var verificacao = totpService.Verificar(secret, codigo, mfa.UltimoTimeStep);
        if (!verificacao.Valido)
            return Result.Failure(MfaErrors.CodigoInvalido);

        return mfa.RegistrarUso(verificacao.TimeStep, agora);
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
