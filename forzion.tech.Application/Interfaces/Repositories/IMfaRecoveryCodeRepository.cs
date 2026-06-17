using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IMfaRecoveryCodeRepository
{
    Task AdicionarRangeAsync(IEnumerable<MfaRecoveryCode> codes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MfaRecoveryCode>> ListarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
}
