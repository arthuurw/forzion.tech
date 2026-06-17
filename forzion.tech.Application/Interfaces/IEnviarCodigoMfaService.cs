using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces;

public interface IEnviarCodigoMfaService
{
    Task EnviarAsync(Conta conta, MfaProposito proposito, CancellationToken cancellationToken = default);
}
