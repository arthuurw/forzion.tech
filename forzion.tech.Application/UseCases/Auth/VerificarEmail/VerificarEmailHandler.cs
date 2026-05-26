using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

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
    public virtual Task HandleAsync(
        VerificarEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        VerificarEmailCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var hash = ComputeHash(command.Token);

        var token = await tokenRepository.BuscarPorHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (token is null || token.VerifiedAt.HasValue)
            throw new DomainException("Token inválido ou já utilizado.");

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (token.ExpiresAt < agora)
            throw new DomainException("Token expirado. Solicite um novo e-mail de verificação.");

        var conta = await contaRepository.ObterPorIdAsync(token.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Conta não encontrada.");

        conta.MarcarEmailVerificado(agora);
        token.MarcarComoVerificado(agora);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
