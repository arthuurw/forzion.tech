using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task AdicionarAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Marca o token usado atomicamente. Retorna linhas afetadas: 1 = venceu a corrida; 0 = reuse.</summary>
    Task<int> MarcarUsadoSeNaoUsadoAsync(Guid tokenId, DateTime usadoEm, Guid sucessorId, CancellationToken cancellationToken = default);
}
