using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Conta.AlterarSenha;

public record AlterarSenhaCommand(string SenhaAtual, string NovaSenha);

public class AlterarSenhaCommandValidator : AbstractValidator<AlterarSenhaCommand>
{
    public AlterarSenhaCommandValidator()
    {
        RuleFor(x => x.SenhaAtual).NotEmpty();
        RuleFor(x => x.NovaSenha)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A nova senha deve ter pelo menos 8 caracteres.")
            .MaximumLength(72)
            .Matches("[A-Z]").WithMessage("A nova senha deve conter pelo menos uma letra maiúscula.")
            .Matches("[a-z]").WithMessage("A nova senha deve conter pelo menos uma letra minúscula.")
            .Matches("[0-9]").WithMessage("A nova senha deve conter pelo menos um dígito.");
    }
}

public class AlterarSenhaHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AlterarSenhaCommand> validator)
{
    public virtual Task HandleAsync(
        AlterarSenhaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        AlterarSenhaCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Conta autenticada não encontrada.");

        if (!passwordHasher.Verify(command.SenhaAtual, conta.PasswordHash))
            throw new CredenciaisInvalidasException();

        var atualizarResult = conta.AtualizarSenha(passwordHasher.Hash(command.NovaSenha), timeProvider.GetUtcNow().UtcDateTime);
        if (atualizarResult.IsFailure)
            throw new DomainException(atualizarResult.Error!.Message);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
