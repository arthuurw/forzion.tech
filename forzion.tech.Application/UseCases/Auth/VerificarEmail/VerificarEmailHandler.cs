using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Auth.VerificarEmail;

public record VerificarEmailCommand(string Token);

public class VerificarEmailCommandValidator : AbstractValidator<VerificarEmailCommand>
{
    public VerificarEmailCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("O token é obrigatório.")
            .Length(64).WithMessage("Token inválido.");
    }
}

public class VerificarEmailHandler(
    IEmailVerificationTokenRepository tokenRepository,
    IContaRepository contaRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<VerificarEmailCommand> validator)
{
    public virtual Task<Result> HandleAsync(
        VerificarEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        VerificarEmailCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var hash = ComputeHash(command.Token);

        var token = await tokenRepository.BuscarPorHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (token is null || token.VerifiedAt.HasValue)
            return Result.Failure(Error.Business("auth_verify.token_invalido", "Token inválido ou já utilizado."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (token.ExpiresAt < agora)
            return Result.Failure(Error.Business("auth_verify.token_expirado", "Token expirado. Solicite um novo e-mail de verificação."));

        var conta = await contaRepository.ObterPorIdAsync(token.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Conta não encontrada.");

        conta.MarcarEmailVerificado(agora);

        var marcarResult = token.MarcarComoVerificado(agora);
        if (marcarResult.IsFailure)
            return Result.Failure(marcarResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
