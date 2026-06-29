using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Validation;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.AlterarSenha;

public record AlterarSenhaCommand(string SenhaAtual, string NovaSenha);

public class AlterarSenhaCommandValidator : AbstractValidator<AlterarSenhaCommand>
{
    public AlterarSenhaCommandValidator()
    {
        RuleFor(x => x.SenhaAtual).NotEmpty().WithMessage("A senha atual é obrigatória.");
        RuleFor(x => x.NovaSenha).SenhaForte();
    }
}

public class AlterarSenhaHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokenService,
    ITrustedDeviceRepository trustedDeviceRepository,
    ITokenRevogadoRepository tokenRevogadoRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AlterarSenhaCommand> validator,
    ILogger<AlterarSenhaHandler> logger)
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
            ?? throw new EstadoInconsistenteException("Conta autenticada não encontrada.");

        if (!passwordHasher.Verify(command.SenhaAtual, conta.PasswordHash))
            throw new CredenciaisInvalidasException();

        var agoraOffset = timeProvider.GetUtcNow();
        var agora = agoraOffset.UtcDateTime;
        var atualizarResult = conta.AtualizarSenha(passwordHasher.Hash(command.NovaSenha), agora);
        if (atualizarResult.IsFailure)
            return Result.Failure(atualizarResult.Error!);

        conta.InvalidarSessoesAnteriores(agoraOffset);

        // Blacklist do jti corrente: sem ele, o access roubado sobrevive à troca de senha por até 15min.
        await refreshTokenService.RevogarTodasPorContaAsync(conta.Id, MotivoRevogacaoFamilia.TrocaSenha, agora, cancellationToken).ConfigureAwait(false);
        await trustedDeviceRepository.RemoverPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);

        var jti = userContext.Jti;
        var tokenExpiraEm = userContext.TokenExpiraEm;
        if (jti != Guid.Empty && tokenExpiraEm > agora)
        {
            var tokenResult = TokenRevogado.Criar(jti, tokenExpiraEm, agora);
            if (tokenResult.IsFailure)
                return Result.Failure(tokenResult.Error!);
            await tokenRevogadoRepository.AdicionarAsync(tokenResult.Value, cancellationToken).ConfigureAwait(false);
        }

        var logResult = await logRepository.RegistrarAsync(TipoAcaoAprovacao.SenhaAlterada, userContext.ContaId, userContext.ContaId, "Conta", agora, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Senha alterada — conta {ContaId}.", userContext.ContaId);

        return Result.Success();
    }
}
