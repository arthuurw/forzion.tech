using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Conta.Mfa;

public record IniciarEnrollTotpResult(string SecretBase32, string OtpauthUri);

public class IniciarEnrollTotpHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IContaMfaRepository contaMfaRepository,
    ITotpService totpService,
    IMfaSecretProtector secretProtector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private const string Issuer = "forzion.tech";

    public virtual async Task<Result<IniciarEnrollTotpResult>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new EstadoInconsistenteException("Conta autenticada não encontrada.");

        var existente = await contaMfaRepository.BuscarPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
        if (existente is { Habilitado: true })
            return Result.Failure<IniciarEnrollTotpResult>(MfaErrors.JaConfirmado);

        var secret = totpService.GerarSecret();
        var cifrado = secretProtector.Proteger(secret);
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (existente is null)
        {
            var criar = ContaMfa.Criar(conta.Id, cifrado, agora);
            if (criar.IsFailure)
                return Result.Failure<IniciarEnrollTotpResult>(criar.Error!);
            await contaMfaRepository.AdicionarAsync(criar.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var atualizar = existente.AtualizarSecretPendente(cifrado, agora);
            if (atualizar.IsFailure)
                return Result.Failure<IniciarEnrollTotpResult>(atualizar.Error!);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        var uri = totpService.GerarUri(secret, conta.Email.Value, Issuer);
        return Result.Success(new IniciarEnrollTotpResult(secret, uri));
    }
}
