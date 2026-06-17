using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITrustedDeviceRepository
{
    Task AdicionarAsync(TrustedDevice device, CancellationToken cancellationToken = default);
    Task<TrustedDevice?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrustedDevice>> ListarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
}
