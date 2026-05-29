using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Auth.RedefinirSenha;

public record RedefinirSenhaCommand(string Token, string NovaSenha);

public class RedefinirSenhaCommandValidator : AbstractValidator<RedefinirSenhaCommand>
{
    public RedefinirSenhaCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("O token é obrigatório.")
            .Length(64).WithMessage("Token inválido.");

        RuleFor(x => x.NovaSenha)
            .NotEmpty().WithMessage("A nova senha é obrigatória.")
            .MinimumLength(8).WithMessage("A nova senha deve ter pelo menos 8 caracteres.")
            .MaximumLength(72).WithMessage("A nova senha deve ter no máximo 72 caracteres.")
            .Matches("[A-Z]").WithMessage("A nova senha deve conter pelo menos uma letra maiúscula.")
            .Matches("[a-z]").WithMessage("A nova senha deve conter pelo menos uma letra minúscula.")
            .Matches("[0-9]").WithMessage("A nova senha deve conter pelo menos um dígito.");
    }
}

public class RedefinirSenhaHandler(
    IPasswordResetTokenRepository tokenRepository,
    IContaRepository contaRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<RedefinirSenhaCommand> validator)
{
    public virtual Task HandleAsync(
        RedefinirSenhaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        RedefinirSenhaCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var hash = ComputeHash(command.Token);

        var token = await tokenRepository.BuscarPorHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (token is null || token.UsedAt.HasValue)
            throw new DomainException("Token inválido ou já utilizado.");

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (token.ExpiresAt < agora)
            throw new DomainException("Token expirado. Solicite um novo link de redefinição.");

        var conta = await contaRepository.ObterPorIdAsync(token.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Conta não encontrada.");

        var atualizarResult = conta.AtualizarSenha(passwordHasher.Hash(command.NovaSenha), agora);
        if (atualizarResult.IsFailure)
            throw new DomainException(atualizarResult.Error!.Message);

        var marcarResult = token.MarcarComoUsado(agora);
        if (marcarResult.IsFailure)
            throw new DomainException(marcarResult.Error!.Message);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
