using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPlanoRepository
{
    Task<Plano?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Plano?> ObterPlanoFreeAsync(CancellationToken cancellationToken = default);
}
