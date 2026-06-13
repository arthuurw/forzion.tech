using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task AdicionarAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
