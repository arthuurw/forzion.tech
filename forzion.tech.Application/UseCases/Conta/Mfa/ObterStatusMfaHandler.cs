using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Conta.Mfa;

public record MfaStatusResult(bool Habilitado, int RecoveryCodesRestantes, IReadOnlyList<MfaDispositivoConfiavel> Dispositivos);

public record MfaDispositivoConfiavel(Guid Id, string? Rotulo, DateTime CriadoEm, DateTime? UltimoUsoEm, DateTime ExpiraEm);

public class ObterStatusMfaHandler(
    IUserContext userContext,
    IContaMfaRepository contaMfaRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    ITrustedDeviceRepository trustedDeviceRepository,
    TimeProvider timeProvider)
{
    public virtual async Task<MfaStatusResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (mfa is not { Habilitado: true })
            return new MfaStatusResult(false, 0, []);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var recovery = await recoveryCodeRepository.ListarPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        var devices = await trustedDeviceRepository.ListarPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);

        var dispositivos = devices
            .Where(d => d.EstaAtivo(agora))
            .Select(d => new MfaDispositivoConfiavel(d.Id, d.Rotulo, d.CriadoEm, d.UltimoUsoEm, d.ExpiraEm))
            .ToList();

        return new MfaStatusResult(true, recovery.Count(c => c.Disponivel), dispositivos);
    }
}
