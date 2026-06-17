using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Auth.Mfa;

public class SolicitarCodigoLoginEmailHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IContaMfaRepository contaMfaRepository,
    IEnviarCodigoMfaService enviarCodigoMfa)
{
    public virtual async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return;

        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
        if (mfa is not { Habilitado: true })
            return;

        await enviarCodigoMfa.EnviarAsync(conta, MfaProposito.LoginFallback, cancellationToken).ConfigureAwait(false);
    }
}
