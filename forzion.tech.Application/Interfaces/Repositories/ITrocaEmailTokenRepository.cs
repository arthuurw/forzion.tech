using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITrocaEmailTokenRepository
{
    Task AdicionarAsync(TrocaEmailToken token, CancellationToken cancellationToken = default);
    Task<TrocaEmailToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
