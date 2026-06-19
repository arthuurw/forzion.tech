using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public record SolicitarTrocaEmailCommand(Guid ContaId, string NovoEmail);

public class SolicitarTrocaEmailCommandValidator : AbstractValidator<SolicitarTrocaEmailCommand>
{
    public SolicitarTrocaEmailCommandValidator()
    {
        RuleFor(x => x.NovoEmail)
            .NotEmpty().WithMessage("O novo e-mail é obrigatório.")
            .EmailAddress().WithMessage("O novo e-mail é inválido.");
    }
}

public class SolicitarTrocaEmailHandler(
    IContaRepository contaRepository,
    ITrocaEmailTokenRepository tokenRepository,
    IEmailCriticoDispatcher emailCritico,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<SolicitarTrocaEmailHandler> logger,
    IValidator<SolicitarTrocaEmailCommand> validator)
{
    public virtual Task HandleAsync(
        SolicitarTrocaEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        SolicitarTrocaEmailCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var emailResult = Domain.ValueObjects.Email.Criar(command.NovoEmail);
        if (emailResult.IsFailure)
            return;

        var novoEmail = emailResult.Value;

        var conta = await contaRepository.ObterPorIdAsync(command.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Conta autenticada não encontrada.");

        if (string.Equals(conta.Email.Value, novoEmail.Value, StringComparison.OrdinalIgnoreCase))
            return;

        var contaExistente = await contaRepository.ObterPorEmailAsync(novoEmail.Value, cancellationToken).ConfigureAwait(false);
        if (contaExistente is not null)
        {
            logger.LogDebug("SolicitarTrocaEmailHandler: e-mail {NovoEmail} já está em uso — resposta genérica.", novoEmail.Value);
            return;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = GenerateRawToken();
        var tokenHash = ComputeHash(rawToken);

        var tokenResult = TrocaEmailToken.Criar(conta.Id, novoEmail.Value, tokenHash, agora.AddMinutes(30), agora);
        if (tokenResult.IsFailure)
            return;

        await tokenRepository.AdicionarAsync(tokenResult.Value, cancellationToken).ConfigureAwait(false);
        emailCritico.Enfileirar(EmailCriticoTemplate.TrocaEmail, novoEmail.Value, rawToken);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
