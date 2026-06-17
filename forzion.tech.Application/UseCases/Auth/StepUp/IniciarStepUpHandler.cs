using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Auth.StepUp;

public record IniciarStepUpResult(MfaFator Fator);

public class IniciarStepUpHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IContaMfaRepository contaMfaRepository,
    IEnviarCodigoMfaService enviarCodigoMfa)
{
    public virtual async Task<Result<IniciarStepUpResult>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure<IniciarStepUpResult>(MfaErrors.ContaIdInvalido);

        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
        if (mfa is { Habilitado: true })
            return Result.Success(new IniciarStepUpResult(MfaFator.Totp));

        await enviarCodigoMfa.EnviarAsync(conta, MfaProposito.StepUp, cancellationToken).ConfigureAwait(false);
        return Result.Success(new IniciarStepUpResult(MfaFator.Email));
    }
}
