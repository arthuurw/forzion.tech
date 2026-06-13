using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

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
    IRefreshTokenService refreshTokenService,
    ITokenRevogadoRepository tokenRevogadoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AlterarSenhaCommand> validator)
{
    public virtual Task<Result> HandleAsync(
        AlterarSenhaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        AlterarSenhaCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Conta autenticada não encontrada.");

        if (!passwordHasher.Verify(command.SenhaAtual, conta.PasswordHash))
            throw new CredenciaisInvalidasException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var atualizarResult = conta.AtualizarSenha(passwordHasher.Hash(command.NovaSenha), agora);
        if (atualizarResult.IsFailure)
            return Result.Failure(atualizarResult.Error!);

        // Revogação conjunta (NR-6 / security §2): mata o refresh de todos os devices E faz
        // blacklist do jti corrente — sem o blacklist, o access curto roubado sobreviveria à
        // troca de senha por até 15min (janela que o blacklist fecha).
        await refreshTokenService.RevogarTodasPorContaAsync(conta.Id, MotivoRevogacaoFamilia.TrocaSenha, agora, cancellationToken).ConfigureAwait(false);

        var jti = userContext.Jti;
        var tokenExpiraEm = userContext.TokenExpiraEm;
        if (jti != Guid.Empty && tokenExpiraEm > agora)
        {
            var tokenResult = TokenRevogado.Criar(jti, tokenExpiraEm, agora);
            if (tokenResult.IsFailure)
                return Result.Failure(tokenResult.Error!);
            await tokenRevogadoRepository.AdicionarAsync(tokenResult.Value, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
