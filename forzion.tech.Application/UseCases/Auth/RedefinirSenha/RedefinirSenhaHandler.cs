using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Validation;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Auth.RedefinirSenha;

public record RedefinirSenhaCommand(string Token, string NovaSenha);

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
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokenService,
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
            ?? throw new DomainException("Conta não encontrada.");

        var atualizarResult = conta.AtualizarSenha(passwordHasher.Hash(command.NovaSenha), agora);
        if (atualizarResult.IsFailure)
            return Result.Failure(atualizarResult.Error!);

        conta.InvalidarSessoesAnteriores(agoraOffset);

        var marcarResult = token.MarcarComoUsado(agora);
        if (marcarResult.IsFailure)
            return Result.Failure(marcarResult.Error!);

        // Reset de senha revoga todas as sessões da conta (NR-6) — inclui o device de quem roubou a senha.
        await refreshTokenService.RevogarTodasPorContaAsync(conta.Id, MotivoRevogacaoFamilia.TrocaSenha, agora, cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
