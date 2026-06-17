using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Conta.TrocarEmail;

public record ConfirmarTrocaEmailCommand(Guid ContaId, Guid Jti, DateTime TokenExpiraEm, string Codigo);

public class ConfirmarTrocaEmailCommandValidator : AbstractValidator<ConfirmarTrocaEmailCommand>
{
    public ConfirmarTrocaEmailCommandValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("O código é obrigatório.");
    }
}

public class ConfirmarTrocaEmailHandler(
    IContaRepository contaRepository,
    ITrocaEmailTokenRepository tokenRepository,
    IRefreshTokenService refreshTokenService,
    ITrustedDeviceRepository trustedDeviceRepository,
    ITokenRevogadoRepository tokenRevogadoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<ConfirmarTrocaEmailCommand> validator)
{
    public virtual Task<Result> HandleAsync(
        ConfirmarTrocaEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ConfirmarTrocaEmailCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var hash = ComputeHash(command.Codigo);

        var token = await tokenRepository.BuscarPorHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (token is null || token.UsadoEm.HasValue)
            return Result.Failure(Error.Business("troca_email.codigo_invalido", "Código inválido ou já utilizado."));

        var agoraOffset = timeProvider.GetUtcNow();
        var agora = agoraOffset.UtcDateTime;

        if (token.ExpiraEm < agora)
            return Result.Failure(Error.Business("troca_email.codigo_expirado", "Código expirado."));

        if (token.ContaId != command.ContaId)
            return Result.Failure(Error.Business("troca_email.codigo_invalido", "Código inválido ou já utilizado."));

        var conta = await contaRepository.ObterPorIdAsync(token.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Conta não encontrada.");

        var emailResult = Email.Criar(token.NovoEmail);
        if (emailResult.IsFailure)
            return Result.Failure(emailResult.Error!);

        var emailEmUso = await contaRepository.ObterPorEmailAsync(emailResult.Value.Value, cancellationToken).ConfigureAwait(false);
        if (emailEmUso is not null && emailEmUso.Id != conta.Id)
            return Result.Failure(Error.Business("troca_email.email_em_uso", "O e-mail não está mais disponível."));

        var atualizarResult = conta.AtualizarEmail(emailResult.Value, agora);
        if (atualizarResult.IsFailure)
            return Result.Failure(atualizarResult.Error!);

        conta.InvalidarSessoesAnteriores(agoraOffset);

        var marcarResult = token.MarcarComoUsado(agora);
        if (marcarResult.IsFailure)
            return Result.Failure(marcarResult.Error!);

        await refreshTokenService.RevogarTodasPorContaAsync(conta.Id, MotivoRevogacaoFamilia.TrocaEmail, agora, cancellationToken).ConfigureAwait(false);
        await trustedDeviceRepository.RemoverPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);

        var jti = command.Jti;
        var tokenExpiraEm = command.TokenExpiraEm;
        if (jti != Guid.Empty && tokenExpiraEm > agora)
        {
            var tokenRevogadoResult = Domain.Entities.TokenRevogado.Criar(jti, tokenExpiraEm, agora);
            if (tokenRevogadoResult.IsFailure)
                return Result.Failure(tokenRevogadoResult.Error!);
            await tokenRevogadoRepository.AdicionarAsync(tokenRevogadoResult.Value, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
