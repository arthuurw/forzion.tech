using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITokenRevogadoRepository
{
    Task AdicionarAsync(TokenRevogado token, CancellationToken cancellationToken = default);
    Task<bool> EstaRevogadoAsync(Guid jti, CancellationToken cancellationToken = default);
    Task<int> LimparExpiradosAsync(CancellationToken cancellationToken = default);
}
